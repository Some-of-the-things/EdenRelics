using System.Security.Claims;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using Product = Eden_Relics_BE.Data.Entities.Product;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly EdenRelicsDbContext _context;
    private readonly IConfiguration _configuration;

    public OrdersController(EdenRelicsDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<ActionResult> Create(CreateOrderDto dto)
    {
        string? userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = userIdClaim is not null ? Guid.Parse(userIdClaim) : null;

        if (userId is null && string.IsNullOrWhiteSpace(dto.GuestEmail))
        {
            return BadRequest(new { message = "Guest email is required for anonymous checkout." });
        }

        List<Product> products = [];
        foreach (CreateOrderItemDto item in dto.Items)
        {
            Product? product = await _context.Products.FindAsync(item.ProductId);
            if (product is null)
            {
                return BadRequest(new { message = $"Product {item.ProductId} not found." });
            }
            products.Add(product);
        }

        // Determine shipping cost based on zone
        decimal shippingCost = ShippingZones.GetShippingCost(
            dto.ShippingMethod, dto.ShippingAddress?.Country);

        Order order = new()
        {
            UserId = userId,
            GuestEmail = userId is null ? dto.GuestEmail!.Trim().ToLowerInvariant() : null,
            ShippingMethod = dto.ShippingMethod ?? "standard",
            ShippingCost = shippingCost,
            ShipAddressLine1 = dto.ShippingAddress?.AddressLine1,
            ShipAddressLine2 = dto.ShippingAddress?.AddressLine2,
            ShipCity = dto.ShippingAddress?.City,
            ShipCounty = dto.ShippingAddress?.County,
            ShipPostcode = dto.ShippingAddress?.Postcode,
            ShipCountry = dto.ShippingAddress?.Country,
            BillAddressLine1 = dto.BillingAddress?.AddressLine1,
            BillAddressLine2 = dto.BillingAddress?.AddressLine2,
            BillCity = dto.BillingAddress?.City,
            BillCounty = dto.BillingAddress?.County,
            BillPostcode = dto.BillingAddress?.Postcode,
            BillCountry = dto.BillingAddress?.Country,
            Items = dto.Items.Select(item =>
            {
                Product product = products.First(p => p.Id == item.ProductId);
                return new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.SalePrice ?? product.Price,
                    Quantity = item.Quantity
                };
            }).ToList()
        };

        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity) + shippingCost;

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Create Stripe Checkout Session
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        string frontendUrl = _configuration["Stripe:FrontendUrl"] ?? "http://localhost:4200";

        List<SessionLineItemOptions> lineItems = order.Items.Select(item => new SessionLineItemOptions
        {
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = "gbp",
                UnitAmountDecimal = item.UnitPrice * 100,
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = item.ProductName,
                },
            },
            Quantity = item.Quantity,
        }).ToList();

        if (shippingCost > 0)
        {
            lineItems.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "gbp",
                    UnitAmountDecimal = shippingCost * 100,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"Shipping ({order.ShippingMethod})",
                    },
                },
                Quantity = 1,
            });
        }

        SessionCreateOptions sessionOptions = new()
        {
            PaymentMethodTypes = ["card"],
            LineItems = lineItems,
            Mode = "payment",
            SuccessUrl = $"{frontendUrl}/order-confirmation/{order.Id}?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{frontendUrl}/cart",
            Metadata = new Dictionary<string, string>
            {
                { "order_id", order.Id.ToString() }
            },
            ShippingAddressCollection = new SessionShippingAddressCollectionOptions
            {
                AllowedCountries = ["GB"],
            },
            ShippingOptions =
            [
                new SessionShippingOptionOptions
                {
                    ShippingRateData = new SessionShippingOptionShippingRateDataOptions
                    {
                        Type = "fixed_amount",
                        DisplayName = "Free standard delivery",
                        FixedAmount = new SessionShippingOptionShippingRateDataFixedAmountOptions
                        {
                            Amount = 0,
                            Currency = "gbp",
                        },
                        DeliveryEstimate = new SessionShippingOptionShippingRateDataDeliveryEstimateOptions
                        {
                            Minimum = new SessionShippingOptionShippingRateDataDeliveryEstimateMinimumOptions
                            {
                                Unit = "business_day",
                                Value = 3,
                            },
                            Maximum = new SessionShippingOptionShippingRateDataDeliveryEstimateMaximumOptions
                            {
                                Unit = "business_day",
                                Value = 5,
                            },
                        },
                    },
                },
            ],
        };

        if (userId is null)
        {
            sessionOptions.CustomerEmail = dto.GuestEmail!.Trim().ToLowerInvariant();
        }

        SessionService service = new();
        Session session = await service.CreateAsync(sessionOptions);

        order.StripeSessionId = session.Id;
        await _context.SaveChangesAsync();

        return Ok(new CheckoutResponseDto(order.Id, session.Url!));
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        string json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        string endpointSecret = _configuration["Stripe:WebhookSecret"]!;

        try
        {
            Event stripeEvent = EventUtility.ConstructEvent(json,
                Request.Headers["Stripe-Signature"], endpointSecret);

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                Session? session = stripeEvent.Data.Object as Session;
                if (session?.Metadata.TryGetValue("order_id", out string? orderIdStr) == true
                    && Guid.TryParse(orderIdStr, out Guid orderId))
                {
                    Order? order = await _context.Orders
                        .Include(o => o.Items)
                        .FirstOrDefaultAsync(o => o.Id == orderId);
                    if (order is not null)
                    {
                        order.Status = "Paid";

                        // Mark sold products and flag marketplace listings for removal
                        foreach (OrderItem item in order.Items)
                        {
                            Product? product = await _context.Products
                                .Include(p => p.Listings)
                                .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                            if (product is not null)
                            {
                                product.InStock = false;
                                foreach (ProductListing listing in product.Listings.Where(l => l.Status == "Active"))
                                {
                                    listing.Status = listing.Platform == "Website" ? "Sold" : "PendingRemoval";
                                }
                            }
                        }

                        await _context.SaveChangesAsync();
                    }
                }
            }

            return Ok();
        }
        catch (StripeException)
        {
            return BadRequest();
        }
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrders()
    {
        Guid userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        List<Order> orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        return Ok(orders.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id)
    {
        Order? order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        // Verify ownership: authenticated users can only view their own orders
        string? userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is not null)
        {
            Guid userId = Guid.Parse(userIdClaim);
            if (order.UserId != userId && !User.IsInRole("Admin"))
            {
                return NotFound();
            }
        }
        else if (order.UserId is not null)
        {
            // Unauthenticated user trying to access a registered user's order
            return NotFound();
        }

        return Ok(ToDto(order));
    }

    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminOrderDto>>> GetAllOrders()
    {
        List<Order> orders = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        return Ok(orders.Select(ToAdminDto));
    }

    [HttpPut("admin/{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminOrderDto>> UpdateStatus(Guid id, UpdateOrderStatusDto dto)
    {
        Order? order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        order.Status = dto.Status;
        await _context.SaveChangesAsync();

        return Ok(ToAdminDto(order));
    }

    [HttpDelete("admin/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteOrder(Guid id)
    {
        Order? order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        _context.OrderItems.RemoveRange(order.Items);
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id,
        o.Status,
        o.Total,
        o.CreatedAtUtc,
        o.Items.Select(i => new OrderItemDto(
            i.ProductId, i.ProductName, i.UnitPrice, i.Quantity
        )).ToList()
    );

    private static AdminOrderDto ToAdminDto(Order o) => new(
        o.Id,
        o.Status,
        o.Total,
        o.CreatedAtUtc,
        o.User?.Email ?? o.GuestEmail ?? "Unknown",
        o.User is not null ? $"{o.User.FirstName} {o.User.LastName}" : null,
        o.Items.Select(i => new OrderItemDto(
            i.ProductId, i.ProductName, i.UnitPrice, i.Quantity
        )).ToList()
    );
}
