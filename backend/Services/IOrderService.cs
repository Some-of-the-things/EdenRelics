using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface IOrderService
{
    Task<CreateOrderResult> CreateAsync(CreateOrderDto dto, Guid? userId);
    Task<bool> HandleWebhookAsync(string json, string signature);
    Task<List<OrderDto>> GetMyOrdersAsync(Guid userId);
    Task<OrderDto?> GetByIdForViewerAsync(Guid id, Guid? userId, bool isAdmin);
    Task<List<AdminOrderDto>> GetAllForAdminAsync();
    Task<AdminOrderDto?> UpdateStatusAsync(Guid id, UpdateOrderStatusDto dto);
    Task<SendInvoiceResult> SendInvoiceAsync(Guid id, string? platform);
    Task<string?> BuildInvoicePreviewAsync(Guid id, string? platform);
    Task<bool> DeleteAsync(Guid id);
}

public record CreateOrderResult(CheckoutResponseDto? Response, string? Error);

public enum SendInvoiceOutcome { NotFound, NoEmail, Sent, Failed }
public record SendInvoiceResult(SendInvoiceOutcome Outcome, string? Recipient);
