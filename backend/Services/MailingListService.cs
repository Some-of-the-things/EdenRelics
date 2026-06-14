using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;

namespace Eden_Relics_BE.Services;

public class MailingListService(IRepository<MailingListSubscriber> repository) : IMailingListService
{
    public async Task SubscribeAsync(MailingListSubscribeDto dto)
    {
        string email = dto.Email.Trim().ToLowerInvariant();

        MailingListSubscriber? existing = (await repository.FindAsync(m => m.Email == email)).FirstOrDefault();
        if (existing is not null)
        {
            // Re-subscribe a previously unsubscribed address rather than duplicating it.
            if (existing.Unsubscribed)
            {
                existing.Unsubscribed = false;
                existing.Source = dto.Source ?? "Homepage";
                await repository.UpdateAsync(existing);
            }
            return;
        }

        await repository.AddAsync(new MailingListSubscriber
        {
            Email = email,
            FirstName = dto.FirstName?.Trim(),
            Source = dto.Source ?? "Homepage",
        });
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
