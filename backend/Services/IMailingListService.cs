using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface IMailingListService
{
    /// <summary>Returns false when the address isn't a usable email address.</summary>
    Task<bool> SubscribeAsync(MailingListSubscribeDto dto);
    Task UnsubscribeAsync(string email);

    /// <summary>One-click unsubscribe from a newsletter link or header. False if the token is unknown.</summary>
    Task<bool> UnsubscribeByTokenAsync(string token);

    /// <summary>Undo from the unsubscribe page. False if the token is unknown.</summary>
    Task<bool> ResubscribeByTokenAsync(string token);

    Task<List<MailingListSubscriberDto>> GetActiveAsync();
    Task<int> GetActiveCountAsync();

    Task<List<AdminSubscriberDto>> GetForAdminAsync(string? search, string? status);
    Task<bool> MarkUnsubscribedAsync(Guid id);
    Task<bool> RemoveAsync(Guid id);
}

public record MailingListSubscribeDto(string Email, string? FirstName, string? Source);
public record MailingListUnsubscribeDto(string Email);
public record MailingListSubscriberDto(Guid Id, string Email, string? FirstName, string Source, DateTime CreatedAtUtc);
