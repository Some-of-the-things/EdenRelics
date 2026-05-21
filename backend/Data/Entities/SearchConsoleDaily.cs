namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// Per-day aggregated totals from Google Search Console (Search Analytics API).
/// One row per Date. Insert/update is idempotent on Date.
/// </summary>
public class SearchConsoleDailyTotal : BaseEntity
{
    public DateOnly Date { get; set; }
    public int Clicks { get; set; }
    public int Impressions { get; set; }
    public double Ctr { get; set; }
    public double Position { get; set; }
}

/// <summary>
/// Per-day, per-query metrics from Search Console. Lets us see which queries
/// are showing impressions, where we rank for them, and how CTR is trending.
/// Unique on (Date, Query). Position is the weighted average for that day.
/// </summary>
public class SearchConsoleDailyQuery : BaseEntity
{
    public DateOnly Date { get; set; }
    public required string Query { get; set; }
    public int Clicks { get; set; }
    public int Impressions { get; set; }
    public double Ctr { get; set; }
    public double Position { get; set; }
}

/// <summary>
/// Per-day, per-page metrics from Search Console. Shows which pages are
/// being surfaced in search and how they perform.
/// Unique on (Date, Page).
/// </summary>
public class SearchConsoleDailyPage : BaseEntity
{
    public DateOnly Date { get; set; }
    public required string Page { get; set; }
    public int Clicks { get; set; }
    public int Impressions { get; set; }
    public double Ctr { get; set; }
    public double Position { get; set; }
}
