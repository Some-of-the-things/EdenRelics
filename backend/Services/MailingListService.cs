using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;

namespace Eden_Relics_BE.Services;

public class MailingListService(
    IRepository<MailingListSubscriber> repository,
    IEmailService email) : IMailingListService
{
    /// <summary>Source value set by the shop discount pop-up — these signups get the welcome code.</summary>
    private const string DiscountPopupSource = "Discount Popup";

    /// <summary>Must stay in sync with the frontend pop-up code and the Stripe promotion code.</summary>
    private const string WelcomeDiscountCode = "WELCOME15";

    public async Task SubscribeAsync(MailingListSubscribeDto dto)
    {
        string address = dto.Email.Trim().ToLowerInvariant();
        string source = dto.Source ?? "Homepage";

        MailingListSubscriber? existing = (await repository.FindAsync(m => m.Email == address)).FirstOrDefault();
        if (existing is not null)
        {
            // Re-subscribe a previously unsubscribed address rather than duplicating it.
            if (existing.Unsubscribed)
            {
                existing.Unsubscribed = false;
                existing.Source = source;
                await repository.UpdateAsync(existing);
                await MaybeSendWelcomeCodeAsync(source, address);
            }
            return;
        }

        await repository.AddAsync(new MailingListSubscriber
        {
            Email = address,
            FirstName = dto.FirstName?.Trim(),
            Source = source,
        });
        await MaybeSendWelcomeCodeAsync(source, address);
    }

    /// <summary>Emails the first-order discount code, but only to discount-pop-up signups.</summary>
    private async Task MaybeSendWelcomeCodeAsync(string source, string address)
    {
        if (source == DiscountPopupSource)
        {
            await email.SendDiscountWelcomeEmailAsync(address, WelcomeDiscountCode);
        }
    }

    public async Task UnsubscribeAsync(string email)
    {
        string normalized = email.Trim().ToLowerInvariant();
        MailingListSubscriber? sub = (await repository.FindAsync(m => m.Email == normalized)).FirstOrDefault();
        if (sub is not null)
        {
            sub.Unsubscribed = true;
            await repository.UpdateAsync(sub);
        }
    }

    public async Task<List<MailingListSubscriberDto>> GetActiveAsync()
    {
        IEnumerable<MailingListSubscriber> subs = await repository.FindAsync(m => !m.Unsubscribed);
        return subs
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(s => new MailingListSubscriberDto(s.Id, s.Email, s.FirstName, s.Source, s.CreatedAtUtc))
            .ToList();
    }

    public async Task<int> GetActiveCountAsync()
    {
        return await repository.CountAsync(m => !m.Unsubscribed);
    }
}
