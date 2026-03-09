using System.Security.Claims;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly EdenRelicsDbContext _context;

    public OrdersController(EdenRelicsDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto dto)
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

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, ToDto(order));
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

        if (order is null) return NotFound();
        return Ok(ToDto(order));
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
}
