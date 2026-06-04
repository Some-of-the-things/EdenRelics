using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

public partial class SeoController
{
    /// <summary>A feed counts as stale once its newest day is older than this many days behind today.</summary>
    private const int TrafficStaleDays = 3;

    [HttpGet("traffic/status")]
    public async Task<ActionResult<TrafficStatusDto>> GetTrafficStatus(
        [FromServices] GoogleSearchConsoleService gsc,
        [FromServices] GoogleAnalyticsService ga4,
        CancellationToken ct = default)
    {
        DateOnly? gscLast = await context.SearchConsoleDailyTotals
            .OrderByDescending(t => t.Date).Select(t => (DateOnly?)t.Date).FirstOrDefaultAsync(ct);
        DateOnly? ga4Last = await context.AnalyticsDailyTotals
            .OrderByDescending(t => t.Date).Select(t => (DateOnly?)t.Date).FirstOrDefaultAsync(ct);

        DateOnly staleBefore = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-TrafficStaleDays);
        bool gscStale = gsc.IsConfigured && (gscLast is null || gscLast < staleBefore);
        bool ga4Stale = ga4.IsConfigured && (ga4Last is null || ga4Last < staleBefore);

        return Ok(new TrafficStatusDto(
            gsc.IsConfigured, ga4.IsConfigured, gscLast, ga4Last, gscStale, ga4Stale));
    }

    [HttpGet("traffic/overview")]
    public async Task<ActionResult<TrafficOverviewDto>> GetTrafficOverview([FromQuery] int days = 30)
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<SearchConsoleDailyTotal> gscDays = await context.SearchConsoleDailyTotals
            .Where(t => t.Date >= since)
            .OrderBy(t => t.Date)
            .ToListAsync();

        List<AnalyticsDailyTotal> gaDays = await context.AnalyticsDailyTotals
            .Where(t => t.Date >= since)
            .OrderBy(t => t.Date)
            .ToListAsync();

        return Ok(new TrafficOverviewDto(
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
                gaDays.Sum(d => d.Conversions))));
    }

    [HttpGet("traffic/queries")]
    public async Task<ActionResult<List<QueryRollupDto>>> GetTopQueries(
        [FromQuery] int days = 28,
        [FromQuery] int limit = 100,
        [FromQuery] string sort = "clicks")
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<SearchConsoleDailyQuery> rows = await context.SearchConsoleDailyQueries
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

        return Ok(rolled.Take(Math.Clamp(limit, 1, 1000)).ToList());
    }

    [HttpGet("traffic/pages")]
    public async Task<ActionResult<List<PageRollupDto>>> GetTopPages(
        [FromQuery] int days = 28,
        [FromQuery] int limit = 100,
        [FromQuery] string sort = "clicks")
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<SearchConsoleDailyPage> rows = await context.SearchConsoleDailyPages
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

        return Ok(rolled.Take(Math.Clamp(limit, 1, 1000)).ToList());
    }

    [HttpGet("traffic/sources")]
    public async Task<ActionResult<List<SourceRollupDto>>> GetTopSources(
        [FromQuery] int days = 28,
        [FromQuery] int limit = 100)
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<AnalyticsDailySource> rows = await context.AnalyticsDailySources
            .Where(s => s.Date >= since)
            .ToListAsync();

        List<SourceRollupDto> rolled = rows
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

        return Ok(rolled);
    }

    [HttpGet("traffic/landing-pages")]
    public async Task<ActionResult<List<LandingPageRollupDto>>> GetTopLandingPages(
        [FromQuery] int days = 28,
        [FromQuery] int limit = 100)
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<AnalyticsDailyLandingPage> rows = await context.AnalyticsDailyLandingPages
            .Where(l => l.Date >= since)
            .ToListAsync();

        List<LandingPageRollupDto> rolled = rows
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

        return Ok(rolled);
    }

    /// <summary>
    /// Queries with meaningful impressions but stuck on pages 2-3 — these are
    /// often the cheapest wins. Improving title/meta/internal-link/content for
    /// these can push them onto page 1.
    /// </summary>
    [HttpGet("traffic/opportunities")]
    public async Task<ActionResult<List<QueryRollupDto>>> GetOpportunities(
        [FromQuery] int days = 28,
        [FromQuery] int minImpressions = 10,
        [FromQuery] double minPosition = 5,
        [FromQuery] double maxPosition = 30)
    {
        DateOnly since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(days, 1, 365));

        List<SearchConsoleDailyQuery> rows = await context.SearchConsoleDailyQueries
            .Where(q => q.Date >= since)
            .ToListAsync();

        List<QueryRollupDto> rolled = rows
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

        return Ok(rolled);
    }

    [HttpPost("traffic/run")]
    public async Task<ActionResult<TrafficRunResultDto>> RunTrafficIngest(
        [FromServices] GoogleSearchConsoleService gsc,
        [FromServices] GoogleAnalyticsService ga4,
        [FromQuery] int? days = null)
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

        int gscDays = await context.SearchConsoleDailyTotals.CountAsync();
        int gaDays = await context.AnalyticsDailyTotals.CountAsync();
        return Ok(new TrafficRunResultDto(gscDays, gaDays));
    }
}

public record TrafficStatusDto(
    bool GscConfigured, bool Ga4Configured,
    DateOnly? GscLastDate, DateOnly? Ga4LastDate,
    bool GscStale, bool Ga4Stale);

public record GscDailyDto(DateOnly Date, int Clicks, int Impressions, double Ctr, double Position);
public record GaDailyDto(
    DateOnly Date, int Sessions, int Users, int NewUsers, int EngagedSessions,
    int Conversions, double EngagementRate, double AverageSessionDuration, int ScreenPageViews);
public record TotalsSummaryDto(
    int GscClicks, int GscImpressions, double GscCtr, double GscPosition,
    int GaSessions, int GaUsers, int GaConversions);
public record TrafficOverviewDto(
    List<GscDailyDto> Gsc, List<GaDailyDto> Ga4, TotalsSummaryDto Totals);

public record QueryRollupDto(string Query, int Clicks, int Impressions, double Ctr, double Position);
public record PageRollupDto(string Page, int Clicks, int Impressions, double Ctr, double Position);
public record SourceRollupDto(string Source, string Medium, int Sessions, int Users, int Conversions);
public record LandingPageRollupDto(
    string LandingPage, int Sessions, int EngagedSessions, int Conversions, double AverageSessionDuration);
public record TrafficRunResultDto(int GscDayCount, int Ga4DayCount);
