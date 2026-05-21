using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.SearchConsole.v1;
using Google.Apis.SearchConsole.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Pulls daily totals, per-query, and per-page metrics from Google Search Console
/// (Search Analytics API). Upserts into the DB so re-runs over the same range
/// are idempotent — we wipe the date range and bulk-insert.
/// Auth: OAuth refresh-token flow (queries GSC as the user who originally
/// consented). Service-account auth was abandoned because Workspace orgs
/// block adding external service accounts as GSC users.
/// </summary>
public class GoogleSearchConsoleService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<GoogleSearchConsoleService> logger)
{
    private const string ReadonlyScope = "https://www.googleapis.com/auth/webmasters.readonly";
    private const int MaxRowsPerQuery = 25000;

    private string SiteUrl => configuration["Google:SearchConsole:SiteUrl"] ?? "sc-domain:edenrelics.co.uk";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Google:OAuth:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Google:OAuth:ClientSecret"])
        && !string.IsNullOrWhiteSpace(configuration["Google:OAuth:RefreshToken"]);

    private SearchConsoleService CreateClient()
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

        return new SearchConsoleService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "EdenRelics-SEO",
        });
    }

    /// <summary>
    /// Pulls totals, queries, and pages for [startDate, endDate] (inclusive),
    /// deletes any existing rows in that range, then bulk-inserts fresh data.
    /// </summary>
    public async Task IngestRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogInformation("GSC ingest skipped — service account not configured");
            return;
        }
        if (endDate < startDate)
        {
            return;
        }

        using SearchConsoleService client = CreateClient();
        string start = startDate.ToString("yyyy-MM-dd");
        string end = endDate.ToString("yyyy-MM-dd");

        logger.LogInformation("GSC ingest: {Start} -> {End} for {Site}", start, end, SiteUrl);

        IList<ApiDataRow> totalRows = await QueryAsync(client, start, end, ["date"], ct);
        IList<ApiDataRow> queryRows = await QueryAsync(client, start, end, ["date", "query"], ct);
        IList<ApiDataRow> pageRows = await QueryAsync(client, start, end, ["date", "page"], ct);

        using IServiceScope scope = scopeFactory.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

        // Wipe the range we're about to write so re-runs are idempotent.
        await db.SearchConsoleDailyTotals
            .Where(t => t.Date >= startDate && t.Date <= endDate)
            .ExecuteDeleteAsync(ct);
        await db.SearchConsoleDailyQueries
            .Where(q => q.Date >= startDate && q.Date <= endDate)
            .ExecuteDeleteAsync(ct);
        await db.SearchConsoleDailyPages
            .Where(p => p.Date >= startDate && p.Date <= endDate)
            .ExecuteDeleteAsync(ct);

        List<SearchConsoleDailyTotal> totals = totalRows
            .Select(r => new SearchConsoleDailyTotal
            {
                Date = DateOnly.Parse(r.Keys[0]),
                Clicks = (int)(r.Clicks ?? 0),
                Impressions = (int)(r.Impressions ?? 0),
                Ctr = r.Ctr ?? 0,
                Position = r.Position ?? 0,
            })
            .ToList();

        List<SearchConsoleDailyQuery> queries = queryRows
            .Select(r => new SearchConsoleDailyQuery
            {
                Date = DateOnly.Parse(r.Keys[0]),
                Query = TruncateString(r.Keys[1], 500),
                Clicks = (int)(r.Clicks ?? 0),
                Impressions = (int)(r.Impressions ?? 0),
                Ctr = r.Ctr ?? 0,
                Position = r.Position ?? 0,
            })
            .ToList();

        List<SearchConsoleDailyPage> pages = pageRows
            .Select(r => new SearchConsoleDailyPage
            {
                Date = DateOnly.Parse(r.Keys[0]),
                Page = TruncateString(r.Keys[1], 1000),
                Clicks = (int)(r.Clicks ?? 0),
                Impressions = (int)(r.Impressions ?? 0),
                Ctr = r.Ctr ?? 0,
                Position = r.Position ?? 0,
            })
            .ToList();

        db.SearchConsoleDailyTotals.AddRange(totals);
        db.SearchConsoleDailyQueries.AddRange(queries);
        db.SearchConsoleDailyPages.AddRange(pages);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "GSC ingest complete: {Totals} day totals, {Queries} query rows, {Pages} page rows",
            totals.Count, queries.Count, pages.Count);
    }

    /// <summary>
    /// On first run (no data): backfill the previous 90 days.
    /// On subsequent runs: re-pull the last 7 days to catch revised data.
    /// GSC data lags ~2-3 days, so we always end at today - 2.
    /// </summary>
    public async Task SyncAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogInformation("GSC sync skipped — service account not configured");
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

        DateOnly endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);
        bool hasData = await db.SearchConsoleDailyTotals.AnyAsync(ct);
        DateOnly startDate = hasData
            ? endDate.AddDays(-7)
            : endDate.AddDays(-90);

        await IngestRangeAsync(startDate, endDate, ct);
    }

    private async Task<IList<ApiDataRow>> QueryAsync(
        SearchConsoleService client,
        string start,
        string end,
        string[] dimensions,
        CancellationToken ct)
    {
        List<ApiDataRow> all = [];
        int startRow = 0;
        while (true)
        {
            SearchAnalyticsQueryRequest req = new()
            {
                StartDate = start,
                EndDate = end,
                Dimensions = dimensions,
                RowLimit = MaxRowsPerQuery,
                StartRow = startRow,
                DataState = "all",
            };
            SearchAnalyticsQueryResponse resp = await client.Searchanalytics
                .Query(req, SiteUrl)
                .ExecuteAsync(ct);

            if (resp.Rows is null || resp.Rows.Count == 0)
            {
                break;
            }
            all.AddRange(resp.Rows);
            if (resp.Rows.Count < MaxRowsPerQuery)
            {
                break;
            }
            startRow += MaxRowsPerQuery;
        }
        return all;
    }

    private static string TruncateString(string value, int max)
    {
        return value.Length <= max ? value : value[..max];
    }
}
