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
            return BadRequest(new { message = "Guest email is required for anonymous checkout." });

        List<Product> products = [];
        foreach (CreateOrderItemDto item in dto.Items)
        {
            Product? product = await _context.Products.FindAsync(item.ProductId);
            if (product is null)
                return BadRequest(new { message = $"Product {item.ProductId} not found." });
            products.Add(product);
        }

        Order order = new()
        {
            UserId = userId,
            GuestEmail = userId is null ? dto.GuestEmail!.Trim().ToLowerInvariant() : null,
            Items = dto.Items.Select(item =>
            {
                Product product = products.First(p => p.Id == item.ProductId);
                return new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = item.Quantity
                };
            }).ToList()
        };

        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Create Stripe Checkout Session
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        string frontendUrl = _configuration["Stripe:FrontendUrl"] ?? "http://localhost:4200";

        var lineItems = order.Items.Select(item => new SessionLineItemOptions
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

        var sessionOptions = new SessionCreateOptions
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
        };

        if (userId is null)
        {
            sessionOptions.CustomerEmail = dto.GuestEmail!.Trim().ToLowerInvariant();
        }

        var service = new SessionService();
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
                var session = stripeEvent.Data.Object as Session;
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
