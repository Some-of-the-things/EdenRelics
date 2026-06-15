using Eden_Relics_BE.Data.Entities;

namespace Eden_Relics_BE.Services;

public interface ISeoService
{
    // Health snapshots
    Task<List<SeoHealthSnapshot>> GetHealthSnapshotsAsync(int take);
    Task<SeoHealthSnapshot?> GetLatestHealthSnapshotAsync();
    Task<SeoHealthSnapshot> RunHealthSnapshotAsync();

    // On-page analysis
    Task<SeoAnalysisResult?> AnalyseAsync(string url);

    // Tracked keywords
    Task<List<TrackedKeywordDto>> GetTrackedKeywordsAsync();
    Task<TrackedKeywordDto> AddKeywordAsync(CreateTrackedKeywordDto dto);
    Task<List<TrackedKeywordDto>> CheckAllKeywordsAsync();
    Task<TrackedKeywordDto?> UpdateKeywordAsync(Guid id, UpdateTrackedKeywordDto dto);
    Task<bool> DeleteKeywordAsync(Guid id);

    // Traffic (GSC + GA4)
    Task<TrafficStatusDto> GetTrafficStatusAsync();
    Task<TrafficOverviewDto> GetTrafficOverviewAsync(int days);
    Task<List<QueryRollupDto>> GetTopQueriesAsync(int days, int limit, string sort);
    Task<List<PageRollupDto>> GetTopPagesAsync(int days, int limit, string sort);
    Task<List<SourceRollupDto>> GetTopSourcesAsync(int days, int limit);
    Task<List<LandingPageRollupDto>> GetTopLandingPagesAsync(int days, int limit);
    Task<List<QueryRollupDto>> GetOpportunitiesAsync(int days, int minImpressions, double minPosition, double maxPosition);
    Task<TrafficRunResultDto> RunTrafficIngestAsync(int? days);

    // First-party cookieless page views (bot-filtered)
    Task<PageViewStatsDto> GetPageViewStatsAsync(int days, int limit);
}

public record SeoAnalyseRequest(string Url);

public record SeoAnalysisResult(
    string Url,
    string? Title,
    string? MetaDescription,
    string? MetaKeywords,
    string? CanonicalUrl,
    OpenGraphInfo OpenGraph,
    List<HeadingInfo> Headings,
    int WordCount,
    int ImageCount,
    int ImagesMissingAlt,
    int InternalLinks,
    int ExternalLinks,
    List<string> Issues,
    List<string> Warnings,
    List<string> Passed,
    List<KeywordSuggestion> SuggestedKeywords
);

public record OpenGraphInfo(string? Title, string? Description, string? Image);
public record HeadingInfo(int Level, string Text);
public record KeywordSuggestion(string Keyword, double Score, int Frequency);

public record TrackedKeywordDto(Guid Id, string Keyword, string PageUrl, int? LastPosition, DateTime? LastCheckedUtc, string? Notes);
public record CreateTrackedKeywordDto(string Keyword, string PageUrl, int? Position, string? Notes);
public record UpdateTrackedKeywordDto(string? Keyword, string? PageUrl, int? Position, string? Notes);

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

// First-party page-view analytics (our own cookieless counter, bot-filtered).
public record PageViewStatsDto(
    int Days,
    long HumanViews,
    long BotViews,
    DateOnly? LastDataDate,
    List<PageViewDailyDto> Daily,
    List<PageViewPathDto> TopPages,
    List<PageViewCountryDto> TopCountries);

public record PageViewDailyDto(DateOnly Date, long Humans, long Bots);
public record PageViewPathDto(string Path, long Humans, long Bots);
public record PageViewCountryDto(string Country, long Humans);
