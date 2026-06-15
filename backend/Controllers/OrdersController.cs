using System.Security.Claims;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(IOrderService orders) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult> Create(CreateOrderDto dto)
    {
        string? userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = userIdClaim is not null ? Guid.Parse(userIdClaim) : null;

        CreateOrderResult result = await orders.CreateAsync(dto, userId);
        return result.Error is not null
            ? BadRequest(new { message = result.Error })
            : Ok(result.Response);
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        string json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        string signature = Request.Headers["Stripe-Signature"].ToString();

        bool handled = await orders.HandleWebhookAsync(json, signature);
        return handled ? Ok() : BadRequest();
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrders()
    {
        Guid userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await orders.GetMyOrdersAsync(userId));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id)
    {
        string? userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = userIdClaim is not null ? Guid.Parse(userIdClaim) : null;

        OrderDto? order = await orders.GetByIdForViewerAsync(id, userId, User.IsInRole("Admin"));
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminOrderDto>>> GetAllOrders()
    {
        return Ok(await orders.GetAllForAdminAsync());
    }

    [HttpPut("admin/{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminOrderDto>> UpdateStatus(Guid id, UpdateOrderStatusDto dto)
    {
        AdminOrderDto? updated = await orders.UpdateStatusAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("admin/{id:guid}/send-invoice")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SendInvoice(Guid id, [FromBody] SendInvoiceRequest? body = null)
    {
        SendInvoiceResult result = await orders.SendInvoiceAsync(id, body?.Platform);
        return result.Outcome switch
        {
            SendInvoiceOutcome.NotFound => NotFound(),
            SendInvoiceOutcome.NoEmail => BadRequest(new { error = "This order has no customer email address to send the invoice to." }),
            SendInvoiceOutcome.Sent => Ok(new { sentTo = result.Recipient }),
            _ => StatusCode(502, new { error = "The invoice email could not be sent. Please try again." }),
        };
    }

    /// <summary>Renders the invoice email as HTML for previewing in the browser. Does not send anything.</summary>
    [HttpGet("admin/{id:guid}/invoice-preview")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PreviewInvoice(Guid id, [FromQuery] string? platform = null)
    {
        string? html = await orders.BuildInvoicePreviewAsync(id, platform);
        return html is null ? NotFound() : Content(html, "text/html");
    }

    [HttpDelete("admin/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteOrder(Guid id)
    {
        return await orders.DeleteAsync(id) ? NoContent() : NotFound();
    }
}
