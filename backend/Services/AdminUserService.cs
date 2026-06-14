using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class AdminUserService(
    IRepository<User> users,
    IRepository<Favourite> favourites,
    IRepository<MailingListSubscriber> subscribers,
    IRepository<Order> orders) : IAdminUserService
{
    public async Task<List<AdminUserDto>> GetAllAsync()
    {
        var userRows = await users.Query()
            .OrderByDescending(u => u.CreatedAtUtc)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role,
                u.EmailVerified,
                u.MfaEnabled,
                u.ExternalProvider,
                u.CreatedAtUtc,
            })
            .ToListAsync();

        var favouriteRows = await favourites.Query()
            .Include(f => f.Product)
            .Select(f => new { f.UserId, f.Product.Name })
            .ToListAsync();

        Dictionary<Guid, List<string>> favouritesByUser = favouriteRows
            .GroupBy(f => f.UserId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Name).ToList());

        List<string> subscribedEmails = await subscribers.Query()
            .Where(s => !s.Unsubscribed)
            .Select(s => s.Email.ToLower())
            .ToListAsync();

        HashSet<string> subscribedSet = new(subscribedEmails, StringComparer.OrdinalIgnoreCase);

        var orderCounts = await orders.Query()
            .Where(o => o.UserId != null)
            .GroupBy(o => o.UserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        Dictionary<Guid, int> orderCountByUser = orderCounts.ToDictionary(x => x.UserId, x => x.Count);

        return userRows.Select(u => new AdminUserDto(
            u.Id,
            u.Email,
            u.FirstName,
            u.LastName,
            u.Role,
            u.EmailVerified,
            u.MfaEnabled,
            u.ExternalProvider,
            u.CreatedAtUtc,
            orderCountByUser.GetValueOrDefault(u.Id, 0),
            subscribedSet.Contains(u.Email),
            favouritesByUser.GetValueOrDefault(u.Id, []))).ToList();
    }
}
