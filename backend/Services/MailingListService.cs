using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class MailingListService(
    IRepository<MailingListSubscriber> repository,
    ISignupOfferService signupOffer) : IMailingListService
{
    public async Task<bool> SubscribeAsync(MailingListSubscribeDto dto)
    {
        string? address = NewsletterService.NormaliseEmail(dto.Email);
        if (address is null)
        {
            return false;
        }
        string source = string.IsNullOrWhiteSpace(dto.Source) ? "Homepage" : dto.Source.Trim();
        if (source.Length > 30)
        {
            source = source[..30];
        }

        // Include rows soft-deleted before removal became a real delete: the email column is unique.
        MailingListSubscriber? existing = (await repository.FindAsync(m => m.Email == address, includeDeleted: true)).FirstOrDefault();
        if (existing is not null)
        {
            // Re-subscribe a previously unsubscribed address rather than duplicating it. Source
            // records how they signed up this latest time.
            if (existing.Unsubscribed || existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.Unsubscribed = false;
                existing.UnsubscribedAtUtc = null;
                existing.Source = source;
                existing.UnsubscribeToken ??= NewsletterService.NewToken();
                await repository.UpdateAsync(existing);
                await signupOffer.IssueSignupCodeAsync(address, existing.UnsubscribeToken);
            }
            return true;
        }

        string? firstName = dto.FirstName?.Trim();
        MailingListSubscriber created = new()
        {
            Email = address,
            FirstName = string.IsNullOrEmpty(firstName) ? null : firstName[..Math.Min(firstName.Length, 100)],
            Source = source,
            UnsubscribeToken = NewsletterService.NewToken(),
        };
        await repository.AddAsync(created);
        // Sends the sign-up code when the offer is on (and this address never had one).
        await signupOffer.IssueSignupCodeAsync(address, created.UnsubscribeToken);
        return true;
    }

    public async Task UnsubscribeAsync(string email)
    {
        string normalized = email.Trim().ToLowerInvariant();
        MailingListSubscriber? sub = (await repository.FindAsync(m => m.Email == normalized)).FirstOrDefault();
        if (sub is not null)
        {
            await SetUnsubscribedAsync(sub);
        }
    }

    public async Task<bool> UnsubscribeByTokenAsync(string token)
    {
        MailingListSubscriber? sub = await FindByTokenAsync(token);
        if (sub is null)
        {
            return false;
        }
        await SetUnsubscribedAsync(sub);
        return true;
    }

    public async Task<bool> ResubscribeByTokenAsync(string token)
    {
        MailingListSubscriber? sub = await FindByTokenAsync(token);
        if (sub is null)
        {
            return false;
        }
        if (sub.Unsubscribed)
        {
            sub.Unsubscribed = false;
            sub.UnsubscribedAtUtc = null;
            await repository.UpdateAsync(sub);
        }
        return true;
    }

    private async Task<MailingListSubscriber?> FindByTokenAsync(string token)
    {
        string t = (token ?? string.Empty).Trim();
        if (t.Length is < 16 or > 64)
        {
            return null;
        }
        return await repository.Query().FirstOrDefaultAsync(m => m.UnsubscribeToken == t);
    }

    private async Task SetUnsubscribedAsync(MailingListSubscriber sub)
    {
        if (sub.Unsubscribed)
        {
            return;
        }
        sub.Unsubscribed = true;
        sub.UnsubscribedAtUtc = DateTime.UtcNow;
        await repository.UpdateAsync(sub);
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

    public async Task<List<AdminSubscriberDto>> GetForAdminAsync(string? search, string? status)
    {
        IQueryable<MailingListSubscriber> query = repository.Query();
        if (status == "active")
        {
            query = query.Where(m => !m.Unsubscribed);
        }
        else if (status == "unsubscribed")
        {
            query = query.Where(m => m.Unsubscribed);
        }

        string term = (search ?? string.Empty).Trim().ToLowerInvariant();
        if (term.Length > 0)
        {
            query = query.Where(m => m.Email.Contains(term)
                || (m.FirstName != null && m.FirstName.ToLower().Contains(term))
                || m.Source.ToLower().Contains(term));
        }

        List<MailingListSubscriber> rows = await query.OrderByDescending(m => m.CreatedAtUtc).ToListAsync();
        return rows.Select(m => new AdminSubscriberDto(m.Id, m.Email, m.FirstName, m.Source, m.CreatedAtUtc, m.Unsubscribed, m.UnsubscribedAtUtc)).ToList();
    }

    public async Task<bool> MarkUnsubscribedAsync(Guid id)
    {
        MailingListSubscriber? sub = await repository.GetByIdAsync(id);
        if (sub is null)
        {
            return false;
        }
        await SetUnsubscribedAsync(sub);
        return true;
    }

    public async Task<bool> RemoveAsync(Guid id)
    {
        MailingListSubscriber? sub = await repository.GetByIdAsync(id);
        if (sub is null)
        {
            return false;
        }
        // A real delete (the entity is IHardDeletable): removing someone erases their address.
        await repository.RemoveRangeAsync([sub]);
        return true;
    }
}
