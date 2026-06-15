using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Reports the average position for a tracked keyword over the last LookbackDays
/// using Google Search Console data already ingested into our DB. Replaced the
/// previous Custom Search API approach, which didn't reflect real google.com
/// rankings and was returning 0 positions for every tracked keyword.
/// </summary>
public class RankCheckerService(IServiceScopeFactory scopeFactory, ILogger<RankCheckerService> logger)
{
    private const int LookbackDays = 28;

    /// <summary>
    /// Returns the impressions-weighted average position from GSC over the last
    /// 28 days. Null if the keyword hasn't shown impressions in that window —
    /// which means we don't appear in search for that query yet.
    /// </summary>
    public async Task<int?> CheckRankAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-LookbackDays);
        string needle = keyword.Trim().ToLowerInvariant();

        List<SearchConsoleDailyQuery> rows = await db.SearchConsoleDailyQueries
            .Where(q => q.Date >= since && q.Query.ToLower() == needle)
            .ToListAsync();

        if (rows.Count == 0)
        {
            logger.LogInformation("Keyword '{Keyword}' has no GSC impressions in last {Days} days", keyword, LookbackDays);
            return null;
        }

        long totalImpressions = rows.Sum(r => (long)r.Impressions);
        if (totalImpressions == 0)
        {
            return null;
        }

        double weighted = rows.Sum(r => r.Position * r.Impressions) / totalImpressions;
        int rounded = (int)Math.Round(weighted);
        logger.LogInformation(
            "Keyword '{Keyword}' avg position {Position:F2} over {Days}d ({Impressions} impressions)",
            keyword, weighted, LookbackDays, totalImpressions);
        return rounded;
    }

    public async Task CheckAllKeywordsAsync()
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        EdenRelicsDbContext context = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

        List<TrackedKeyword> keywords = await context.TrackedKeywords
            .Where(k => !k.IsDeleted)
            .ToListAsync();

        logger.LogInformation("Refreshing positions for {Count} tracked keywords from GSC", keywords.Count);

        foreach (TrackedKeyword keyword in keywords)
        {
            int? position = await CheckRankAsync(keyword.Keyword);
            keyword.LastPosition = position;
            keyword.LastCheckedUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Tracked keyword positions refreshed");
    }
}
