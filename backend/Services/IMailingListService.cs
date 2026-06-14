namespace Eden_Relics_BE.Services;

public interface IMailingListService
{
    Task SubscribeAsync(MailingListSubscribeDto dto);
    Task UnsubscribeAsync(string email);
    Task<List<MailingListSubscriberDto>> GetActiveAsync();
    Task<int> GetActiveCountAsync();
}

public record MailingListSubscribeDto(string Email, string? FirstName, string? Source);
public record MailingListUnsubscribeDto(string Email);
public record MailingListSubscriberDto(Guid Id, string Email, string? FirstName, string Source, DateTime CreatedAtUtc);
