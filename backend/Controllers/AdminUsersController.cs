using Eden_Relics_BE.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController(EdenRelicsDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var users = await context.Users
            .Where(u => !u.IsDeleted)
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

        // Load favourites with product names
        var favourites = await context.Favourites
            .Include(f => f.Product)
            .Where(f => !f.IsDeleted)
            .Select(f => new { f.UserId, f.Product.Name })
            .ToListAsync();

        Dictionary<Guid, List<string>> favouritesByUser = favourites
            .GroupBy(f => f.UserId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Name).ToList());

        // Load mailing list subscriptions (match by email)
        var subscribedEmails = await context.MailingListSubscribers
            .Where(s => !s.Unsubscribed && !s.IsDeleted)
            .Select(s => s.Email.ToLower())
            .ToListAsync();

        HashSet<string> subscribedSet = new(subscribedEmails, StringComparer.OrdinalIgnoreCase);

        // Load order counts per user
        var orderCounts = await context.Orders
            .Where(o => o.UserId != null)
            .GroupBy(o => o.UserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        Dictionary<Guid, int> orderCountByUser = orderCounts.ToDictionary(x => x.UserId, x => x.Count);

        var result = users.Select(u => new
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
            OrderCount = orderCountByUser.GetValueOrDefault(u.Id, 0),
            MailingList = subscribedSet.Contains(u.Email),
            Favourites = favouritesByUser.GetValueOrDefault(u.Id, []),
        });

        return Ok(result);
    }
}
