using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Google.Apis.AnalyticsData.v1beta;
using Google.Apis.AnalyticsData.v1beta.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Pulls daily totals, source/medium, and landing-page metrics from GA4 via the
/// Analytics Data API. Like the GSC service, it deletes the date range it's
/// about to write and bulk-inserts so re-runs are idempotent.
/// Auth: OAuth refresh-token flow, same shared client + token as GSC.
/// </summary>
public class GoogleAnalyticsService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<GoogleAnalyticsService> logger)
{
    private const string ReadonlyScope = "https://www.googleapis.com/auth/analytics.readonly";

    private string? PropertyId => configuration["Google:Analytics:PropertyId"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Google:OAuth:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Google:OAuth:ClientSecret"])
        && !string.IsNullOrWhiteSpace(configuration["Google:OAuth:RefreshToken"])
        && !string.IsNullOrWhiteSpace(PropertyId);

    private AnalyticsDataService CreateClient()
    {
        string clientId = configuration["Google:OAuth:ClientId"]
            ?? throw new InvalidOperationException("Google:OAuth:ClientId not configured.");
        string clientSecret = configuration["Google:OAuth:ClientSecret"]
            ?? throw new InvalidOperationException("Google:OAuth:ClientSecret not configured.");
        string refreshToken = configuration["Google:OAuth:RefreshToken"]
            ?? throw new InvalidOperationException("Google:OAuth:RefreshToken not configured.");

        GoogleAuthorizationCodeFlow flow = new(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
            Scopes = [ReadonlyScope],
            DataStore = new NullDataStore(),
        });
        Google.Apis.Auth.OAuth2.UserCredential credential = new(flow, "user", new TokenResponse { RefreshToken = refreshToken });

        return new AnalyticsDataService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "EdenRelics-SEO",
        });
    }

    public async Task IngestRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogInformation("GA4 ingest skipped — service account or property ID not configured");
            return;
        }
        if (endDate < startDate)
        {
            return;
        }

        using AnalyticsDataService client = CreateClient();
        string property = $"properties/{PropertyId}";
        string start = startDate.ToString("yyyy-MM-dd");
        string end = endDate.ToString("yyyy-MM-dd");

        logger.LogInformation("GA4 ingest: {Start} -> {End} for {Property}", start, end, property);

        DateRange range = new() { StartDate = start, EndDate = end };

        // Per-day totals
        RunReportResponse totalsResp = await client.Properties.RunReport(new RunReportRequest
        {
            DateRanges = [range],
            Dimensions = [new Dimension { Name = "date" }],
            Metrics =
            [
                new Metric { Name = "sessions" },
                new Metric { Name = "totalUsers" },
                new Metric { Name = "newUsers" },
                new Metric { Name = "engagedSessions" },
                new Metric { Name = "conversions" },
                new Metric { Name = "engagementRate" },
                new Metric { Name = "averageSessionDuration" },
                new Metric { Name = "screenPageViews" },
            ],
            Limit = 100000,
        }, property).ExecuteAsync(ct);

        // Per-day, per-source/medium
        RunReportResponse sourceResp = await client.Properties.RunReport(new RunReportRequest
        {
            DateRanges = [range],
            Dimensions =
            [
                new Dimension { Name = "date" },
                new Dimension { Name = "sessionSource" },
                new Dimension { Name = "sessionMedium" },
            ],
            Metrics =
            [
                new Metric { Name = "sessions" },
                new Metric { Name = "totalUsers" },
                new Metric { Name = "conversions" },
            ],
            Limit = 100000,
        }, property).ExecuteAsync(ct);

        // Per-day, per-landing-page
        RunReportResponse landingResp = await client.Properties.RunReport(new RunReportRequest
        {
            DateRanges = [range],
            Dimensions =
            [
                new Dimension { Name = "date" },
                new Dimension { Name = "landingPagePlusQueryString" },
            ],
            Metrics =
            [
                new Metric { Name = "sessions" },
                new Metric { Name = "engagedSessions" },
                new Metric { Name = "conversions" },
                new Metric { Name = "averageSessionDuration" },
            ],
            Limit = 100000,
        }, property).ExecuteAsync(ct);

        using IServiceScope scope = scopeFactory.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

        await db.AnalyticsDailyTotals
            .Where(a => a.Date >= startDate && a.Date <= endDate)
            .ExecuteDeleteAsync(ct);
        await db.AnalyticsDailySources
            .Where(a => a.Date >= startDate && a.Date <= endDate)
            .ExecuteDeleteAsync(ct);
        await db.AnalyticsDailyLandingPages
            .Where(a => a.Date >= startDate && a.Date <= endDate)
            .ExecuteDeleteAsync(ct);

        List<AnalyticsDailyTotal> totals = (totalsResp.Rows ?? [])
            .Select(r => new AnalyticsDailyTotal
            {
                Date = ParseGaDate(r.DimensionValues[0].Value),
                Sessions = ParseInt(r.MetricValues[0].Value),
                Users = ParseInt(r.MetricValues[1].Value),
                NewUsers = ParseInt(r.MetricValues[2].Value),
                EngagedSessions = ParseInt(r.MetricValues[3].Value),
                Conversions = ParseInt(r.MetricValues[4].Value),
                EngagementRate = ParseDouble(r.MetricValues[5].Value),
                AverageSessionDuration = ParseDouble(r.MetricValues[6].Value),
                ScreenPageViews = ParseInt(r.MetricValues[7].Value),
            })
            .ToList();

        // Collapse duplicates within the API response (date + source + medium) — GA4
        // can occasionally return separated rows for the same combination.
        List<AnalyticsDailySource> sources = (sourceResp.Rows ?? [])
            .GroupBy(r => (
                Date: ParseGaDate(r.DimensionValues[0].Value),
                Source: TruncateString(r.DimensionValues[1].Value, 200),
                Medium: TruncateString(r.DimensionValues[2].Value, 100)))
            .Select(g => new AnalyticsDailySource
            {
                Date = g.Key.Date,
                Source = g.Key.Source,
                Medium = g.Key.Medium,
                Sessions = g.Sum(r => ParseInt(r.MetricValues[0].Value)),
                Users = g.Sum(r => ParseInt(r.MetricValues[1].Value)),
                Conversions = g.Sum(r => ParseInt(r.MetricValues[2].Value)),
            })
            .ToList();

        List<AnalyticsDailyLandingPage> landings = (landingResp.Rows ?? [])
            .GroupBy(r => (
                Date: ParseGaDate(r.DimensionValues[0].Value),
                LandingPage: TruncateString(r.DimensionValues[1].Value, 1000)))
            .Select(g => new AnalyticsDailyLandingPage
            {
                Date = g.Key.Date,
                LandingPage = g.Key.LandingPage,
                Sessions = g.Sum(r => ParseInt(r.MetricValues[0].Value)),
                EngagedSessions = g.Sum(r => ParseInt(r.MetricValues[1].Value)),
                Conversions = g.Sum(r => ParseInt(r.MetricValues[2].Value)),
                AverageSessionDuration = g.Average(r => ParseDouble(r.MetricValues[3].Value)),
            })
            .ToList();

        db.AnalyticsDailyTotals.AddRange(totals);
        db.AnalyticsDailySources.AddRange(sources);
        db.AnalyticsDailyLandingPages.AddRange(landings);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "GA4 ingest complete: {Totals} day totals, {Sources} source rows, {Landings} landing-page rows",
            totals.Count, sources.Count, landings.Count);
    }

    /// <summary>
    /// First run: backfill 90 days. Subsequent: re-pull last 3 days.
    /// GA4 is usually current to yesterday, so we end at today - 1.
    /// </summary>
    public async Task SyncAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogInformation("GA4 sync skipped — not configured");
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

        DateOnly endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        bool hasData = await db.AnalyticsDailyTotals.AnyAsync(ct);
        DateOnly startDate = hasData
            ? endDate.AddDays(-3)
            : endDate.AddDays(-90);

        await IngestRangeAsync(startDate, endDate, ct);
    }

    private static DateOnly ParseGaDate(string value)
    {
        // GA4 returns dates as yyyyMMdd, not yyyy-MM-dd.
        return DateOnly.ParseExact(value, "yyyyMMdd");
    }

    private static int ParseInt(string? value)
    {
        return int.TryParse(value, out int n) ? n : 0;
    }

    private static double ParseDouble(string? value)
    {
        return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : 0;
    }

    private static string TruncateString(string value, int max)
    {
        return value.Length <= max ? value : value[..max];
    }
}
