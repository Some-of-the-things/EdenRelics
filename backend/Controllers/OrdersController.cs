using System.Security.Claims;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
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
    private readonly IEmailService _emailService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(EdenRelicsDbContext context, IConfiguration configuration, IEmailService emailService, ILogger<OrdersController> logger)
    {
        _context = context;
        _configuration = configuration;
        _emailService = emailService;
        _logger = logger;
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

        if (dto.Items is null || dto.Items.Count == 0)
        {
            return BadRequest(new { message = "Order must contain at least one item." });
        }

        List<Product> products = [];
        foreach (CreateOrderItemDto item in dto.Items)
        {
            // Every product is one-of-one, so quantity is always 1 and an item can't repeat.
            if (item.Quantity != 1)
            {
                return BadRequest(new { message = "Each item is one-of-a-kind; quantity must be 1." });
            }
            if (products.Any(p => p.Id == item.ProductId))
            {
                return BadRequest(new { message = "An item appears more than once in the order." });
            }
            Product? product = await _context.Products.FindAsync(item.ProductId);
            if (product is null)
            {
                return BadRequest(new { message = $"Product {item.ProductId} not found." });
            }
            if (product.Status != ProductStatus.Live)
            {
                return BadRequest(new { message = $"Product {item.ProductId} is not available for purchase." });
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
                        .Include(o => o.User)
                        .FirstOrDefaultAsync(o => o.Id == orderId);
                    if (order is not null)
                    {
                        order.Status = "Paid";

                        // Mark sold products and flag marketplace listings for removal
                        List<Product> soldProducts = [];
                        foreach (OrderItem item in order.Items)
                        {
                            Product? product = await _context.Products
                                .Include(p => p.Listings)
                                .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                            if (product is not null)
                            {
                                product.Status = ProductStatus.Sold;
                                foreach (ProductListing listing in product.Listings.Where(l => l.Status == "Active"))
                                {
                                    listing.Status = listing.Platform == "Website" ? "Sold" : "PendingRemoval";
                                }
                                soldProducts.Add(product);
                            }
                        }

                        // Mirror the sale into the finance ledger. Idempotent by order-id Reference
                        // so Stripe webhook retries don't create duplicates.
                        string orderRef = order.Id.ToString();
                        bool alreadyLogged = await _context.Transactions.AnyAsync(t => t.Reference == orderRef);
                        bool isNewSale = !alreadyLogged;
                        if (isNewSale)
                        {
                            string description = order.Items.Count == 1
                                ? $"Sale: {order.Items[0].ProductName}"
                                : $"Sale: {order.Items.Count} items";
                            _context.Transactions.Add(new Transaction
                            {
                                Date = DateTime.UtcNow,
                                Description = description,
                                Amount = order.Total,
                                Category = "Sales",
                                Platform = "Website",
                                Reference = orderRef,
                            });

                            // Record cost of goods sold: each dress contributes an expense
                            // equal to its cost price, keyed by product so it stays idempotent
                            // (stock is one-of-one, so a dress sells exactly once).
                            foreach (Product soldProduct in soldProducts)
                            {
                                if (soldProduct.CostPrice <= 0) { continue; }
                                string cogsRef = $"cogs:{soldProduct.Id}";
                                if (await _context.Transactions.AnyAsync(t => t.Reference == cogsRef)) { continue; }
                                _context.Transactions.Add(new Transaction
                                {
                                    Date = DateTime.UtcNow,
                                    Description = $"Cost of goods: {soldProduct.Name}",
                                    Amount = -soldProduct.CostPrice,
                                    Category = "Stock",
                                    Reference = cogsRef,
                                });
                            }
                        }

                        await _context.SaveChangesAsync();

                        // Notify the owner once, only on the first time we process this
                        // order's payment. Stripe retries the webhook, and the ledger
                        // Reference acts as the idempotency marker, so guarding on
                        // isNewSale ensures exactly one notification per sale.
                        if (isNewSale)
                        {
                            await _emailService.SendOwnerSaleNotificationAsync(order);

                            // Email the customer their invoice. Never let a delivery failure
                            // fail the webhook — Stripe would retry and double-process the sale.
                            try
                            {
                                await _emailService.SendOrderInvoiceEmailAsync(order);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to send customer invoice for order {OrderId}", order.Id);
                            }
                        }
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

        string previousStatus = order.Status;
        order.Status = dto.Status;

        bool justDelivered =
            order.Status == "Delivered"
            && previousStatus != "Delivered"
            && order.ReviewRequestSentAtUtc is null
            && order.UserId is not null
            && order.User is not null
            && !string.IsNullOrWhiteSpace(order.User.Email);

        if (justDelivered)
        {
            order.ReviewRequestSentAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        if (justDelivered)
        {
            try
            {
                await _emailService.SendReviewRequestEmailAsync(
                    order.User!.Email,
                    order.User.FirstName ?? "",
                    order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send review request email for order {OrderId}", order.Id);
            }
        }

        return Ok(ToAdminDto(order));
    }

    [HttpPost("admin/{id:guid}/send-invoice")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SendInvoice(Guid id, [FromBody] SendInvoiceRequest? body = null)
    {
        Order? order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        string? recipient = order.User?.Email ?? order.GuestEmail;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return BadRequest(new { error = "This order has no customer email address to send the invoice to." });
        }

        try
        {
            await _emailService.SendOrderInvoiceEmailAsync(order, body?.Platform);
            return Ok(new { sentTo = recipient });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual invoice send failed for order {OrderId}", order.Id);
            return StatusCode(502, new { error = "The invoice email could not be sent. Please try again." });
        }
    }

    /// <summary>Renders the invoice email as HTML for previewing in the browser. Does not send anything.</summary>
    [HttpGet("admin/{id:guid}/invoice-preview")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PreviewInvoice(Guid id, [FromQuery] string? platform = null)
    {
        Order? order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        string html = _emailService.BuildOrderInvoiceHtml(order, platform);
        return Content(html, "text/html");
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
