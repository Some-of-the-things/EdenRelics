using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class AnalyticsIngestService(IRepository<PageViewDaily> pageViews) : IAnalyticsIngestService
{
    public async Task RecordPageViewAsync(PageViewBeaconDto beacon)
    {
        string path = NormalisePath(beacon.Path);
        string country = NormaliseCountry(beacon.Country);
        bool isBot = BotClassifier.IsBot(beacon.UserAgent, beacon.AsOrganization);
        DateOnly date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Read-modify-write rather than a SQL upsert: keeps everything inside the
        // repository abstraction and works on the in-memory test provider. Traffic is
        // low enough that the race window is negligible; if two beacons for the same
        // bucket collide on the unique index, we re-read and increment once.
        if (await TryIncrementAsync(date, path, isBot, country))
        {
            return;
        }

        try
        {
            await pageViews.AddAsync(new PageViewDaily
            {
                Date = date,
                Path = path,
                IsBot = isBot,
                Country = country,
                Count = 1,
            });
        }
        catch (DbUpdateException)
        {
            // Lost the insert race — the row now exists, so increment it instead.
            await TryIncrementAsync(date, path, isBot, country);
        }
    }

    private async Task<bool> TryIncrementAsync(DateOnly date, string path, bool isBot, string country)
    {
        PageViewDaily? existing = await pageViews.Query()
            .FirstOrDefaultAsync(p => p.Date == date && p.Path == path && p.IsBot == isBot && p.Country == country);

        if (existing is null)
        {
            return false;
        }

        existing.Count++;
        await pageViews.UpdateAsync(existing);
        return true;
    }

    private static string NormalisePath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "/";
        }

        // Drop query string and fragment so the aggregate is per-page, not per-URL.
        string path = raw.Trim();
        int cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        // Collapse a trailing slash (except the root) so "/about" and "/about/" agree.
        if (path.Length > 1)
        {
            path = path.TrimEnd('/');
            if (path.Length == 0)
            {
                path = "/";
            }
        }

        return path.Length <= 1000 ? path : path[..1000];
    }

    private static string NormaliseCountry(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "ZZ";
        }

        string country = raw.Trim().ToUpperInvariant();
        return country.Length <= 8 ? country : country[..8];
    }
}
