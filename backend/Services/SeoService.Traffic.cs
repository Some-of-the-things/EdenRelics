using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public partial class SeoService
{
    /// <summary>A feed counts as stale once its newest day is older than this many days behind today.</summary>
    private const int TrafficStaleDays = 3;

    public async Task<TrafficStatusDto> GetTrafficStatusAsync()
    {
        DateOnly? gscLast = await scTotals.Query()
            .OrderByDescending(t => t.Date).Select(t => (DateOnly?)t.Date).FirstOrDefaultAsync();
        DateOnly? ga4Last = await gaTotals.Query()
            .OrderByDescending(t => t.Date).Select(t => (DateOnly?)t.Date).FirstOrDefaultAsync();

        DateOnly staleBefore = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-TrafficStaleDays);
        bool gscStale = gsc.IsConfigured && (gscLast is null || gscLast < staleBefore);
        bool ga4Stale = ga4.IsConfigured && (ga4Last is null || ga4Last < staleBefore);

        return new TrafficStatusDto(
            gsc.IsConfigured, ga4.IsConfigured, gscLast, ga4Last, gscStale, ga4Stale);
    }

    public async Task<TrafficOverviewDto> GetTrafficOverviewAsync(int days)
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<SearchConsoleDailyTotal> gscDays = await scTotals.Query()
            .Where(t => t.Date >= since)
            .OrderBy(t => t.Date)
            .ToListAsync();

        List<AnalyticsDailyTotal> gaDays = await gaTotals.Query()
            .Where(t => t.Date >= since)
            .OrderBy(t => t.Date)
            .ToListAsync();

        return new TrafficOverviewDto(
            gscDays.Select(d => new GscDailyDto(d.Date, d.Clicks, d.Impressions, d.Ctr, d.Position)).ToList(),
            gaDays.Select(d => new GaDailyDto(
                d.Date, d.Sessions, d.Users, d.NewUsers, d.EngagedSessions,
                d.Conversions, d.EngagementRate, d.AverageSessionDuration, d.ScreenPageViews)).ToList(),
            new TotalsSummaryDto(
                gscDays.Sum(d => d.Clicks),
                gscDays.Sum(d => d.Impressions),
                gscDays.Sum(d => (long)d.Impressions) > 0
                    ? gscDays.Sum(d => (double)d.Clicks) / gscDays.Sum(d => (double)d.Impressions)
                    : 0,
                gscDays.Sum(d => (long)d.Impressions) > 0
                    ? gscDays.Sum(d => d.Position * d.Impressions) / gscDays.Sum(d => (long)d.Impressions)
                    : 0,
                gaDays.Sum(d => d.Sessions),
                gaDays.Sum(d => d.Users),
                gaDays.Sum(d => d.Conversions)));
    }

    public async Task<List<QueryRollupDto>> GetTopQueriesAsync(int days, int limit, string sort)
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<SearchConsoleDailyQuery> rows = await scQueries.Query()
            .Where(q => q.Date >= since)
            .ToListAsync();

        IEnumerable<QueryRollupDto> rolled = rows
            .GroupBy(r => r.Query)
            .Select(g =>
            {
                long impressions = g.Sum(r => (long)r.Impressions);
                int clicks = g.Sum(r => r.Clicks);
                double position = impressions > 0
                    ? g.Sum(r => r.Position * r.Impressions) / impressions
                    : 0;
                double ctr = impressions > 0 ? (double)clicks / impressions : 0;
                return new QueryRollupDto(g.Key, clicks, (int)impressions, ctr, Math.Round(position, 2));
            });

        rolled = sort switch
        {
            "impressions" => rolled.OrderByDescending(r => r.Impressions),
            "position" => rolled.OrderBy(r => r.Position == 0 ? double.MaxValue : r.Position),
            "ctr" => rolled.OrderByDescending(r => r.Ctr),
            _ => rolled.OrderByDescending(r => r.Clicks).ThenByDescending(r => r.Impressions),
        };

        return rolled.Take(Math.Clamp(limit, 1, 1000)).ToList();
    }

    public async Task<List<PageRollupDto>> GetTopPagesAsync(int days, int limit, string sort)
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<SearchConsoleDailyPage> rows = await scPages.Query()
            .Where(p => p.Date >= since)
            .ToListAsync();

        IEnumerable<PageRollupDto> rolled = rows
            .GroupBy(r => r.Page)
            .Select(g =>
            {
                long impressions = g.Sum(r => (long)r.Impressions);
                int clicks = g.Sum(r => r.Clicks);
                double position = impressions > 0
                    ? g.Sum(r => r.Position * r.Impressions) / impressions
                    : 0;
                double ctr = impressions > 0 ? (double)clicks / impressions : 0;
                return new PageRollupDto(g.Key, clicks, (int)impressions, ctr, Math.Round(position, 2));
            });

        rolled = sort switch
        {
            "impressions" => rolled.OrderByDescending(r => r.Impressions),
            "position" => rolled.OrderBy(r => r.Position == 0 ? double.MaxValue : r.Position),
            "ctr" => rolled.OrderByDescending(r => r.Ctr),
            _ => rolled.OrderByDescending(r => r.Clicks).ThenByDescending(r => r.Impressions),
        };

        return rolled.Take(Math.Clamp(limit, 1, 1000)).ToList();
    }

    public async Task<List<SourceRollupDto>> GetTopSourcesAsync(int days, int limit)
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<AnalyticsDailySource> rows = await gaSources.Query()
            .Where(s => s.Date >= since)
            .ToListAsync();

        return rows
            .GroupBy(r => new { r.Source, r.Medium })
            .Select(g => new SourceRollupDto(
                g.Key.Source,
                g.Key.Medium,
                g.Sum(r => r.Sessions),
                g.Sum(r => r.Users),
                g.Sum(r => r.Conversions)))
            .OrderByDescending(r => r.Sessions)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToList();
    }

    public async Task<List<LandingPageRollupDto>> GetTopLandingPagesAsync(int days, int limit)
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<AnalyticsDailyLandingPage> rows = await gaLandingPages.Query()
            .Where(l => l.Date >= since)
            .ToListAsync();

        return rows
            .GroupBy(r => r.LandingPage)
            .Select(g =>
            {
                int sessions = g.Sum(r => r.Sessions);
                return new LandingPageRollupDto(
                    g.Key,
                    sessions,
                    g.Sum(r => r.EngagedSessions),
                    g.Sum(r => r.Conversions),
                    sessions > 0 ? g.Sum(r => r.AverageSessionDuration * r.Sessions) / sessions : 0);
            })
            .OrderByDescending(r => r.Sessions)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToList();
    }

    public async Task<List<QueryRollupDto>> GetOpportunitiesAsync(int days, int minImpressions, double minPosition, double maxPosition)
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<SearchConsoleDailyQuery> rows = await scQueries.Query()
            .Where(q => q.Date >= since)
            .ToListAsync();

        return rows
            .GroupBy(r => r.Query)
            .Select(g =>
            {
                long impressions = g.Sum(r => (long)r.Impressions);
                int clicks = g.Sum(r => r.Clicks);
                double position = impressions > 0
                    ? g.Sum(r => r.Position * r.Impressions) / impressions
                    : 0;
                double ctr = impressions > 0 ? (double)clicks / impressions : 0;
                return new QueryRollupDto(g.Key, clicks, (int)impressions, ctr, Math.Round(position, 2));
            })
            .Where(r => r.Impressions >= minImpressions
                && r.Position >= minPosition
                && r.Position <= maxPosition)
            .OrderByDescending(r => r.Impressions)
            .ToList();
    }

    public async Task<PageViewStatsDto> GetPageViewStatsAsync(int days, int limit)
    {
        int window = Math.Clamp(days, 1, 365);
        int topLimit = Math.Clamp(limit, 1, 1000);
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-window);

        List<PageViewDaily> rows = await pageViews.Query()
            .Where(p => p.Date >= since)
            .ToListAsync();

        long humanViews = rows.Where(r => !r.IsBot).Sum(r => (long)r.Count);
        long botViews = rows.Where(r => r.IsBot).Sum(r => (long)r.Count);
        DateOnly? lastDataDate = rows.Count > 0 ? rows.Max(r => r.Date) : null;

        List<PageViewDailyDto> daily = rows
            .GroupBy(r => r.Date)
            .Select(g => new PageViewDailyDto(
                g.Key,
                g.Where(r => !r.IsBot).Sum(r => (long)r.Count),
                g.Where(r => r.IsBot).Sum(r => (long)r.Count)))
            .OrderBy(d => d.Date)
            .ToList();

        List<PageViewPathDto> topPages = rows
            .GroupBy(r => r.Path)
            .Select(g => new PageViewPathDto(
                g.Key,
                g.Where(r => !r.IsBot).Sum(r => (long)r.Count),
                g.Where(r => r.IsBot).Sum(r => (long)r.Count)))
            .OrderByDescending(p => p.Humans)
            .ThenByDescending(p => p.Bots)
            .Take(topLimit)
            .ToList();

        List<PageViewCountryDto> topCountries = rows
            .Where(r => !r.IsBot)
            .GroupBy(r => r.Country)
            .Select(g => new PageViewCountryDto(g.Key, g.Sum(r => (long)r.Count)))
            .OrderByDescending(c => c.Humans)
            .Take(topLimit)
            .ToList();

        return new PageViewStatsDto(window, humanViews, botViews, lastDataDate, daily, topPages, topCountries);
    }

    public async Task<TrafficRunResultDto> RunTrafficIngestAsync(int? days)
    {
        if (days.HasValue && days.Value > 0)
        {
            DateOnly endGsc = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);
            DateOnly endGa = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
            int clamped = Math.Clamp(days.Value, 1, 480);
            await gsc.IngestRangeAsync(endGsc.AddDays(-clamped), endGsc);
            await ga4.IngestRangeAsync(endGa.AddDays(-clamped), endGa);
        }
        else
        {
            await gsc.SyncAsync();
            await ga4.SyncAsync();
        }

        int gscDayCount = await scTotals.Query().CountAsync();
        int gaDayCount = await gaTotals.Query().CountAsync();
        return new TrafficRunResultDto(gscDayCount, gaDayCount);
    }
}
