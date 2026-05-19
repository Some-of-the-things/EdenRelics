namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// Per-day aggregated totals from Google Analytics 4 (Analytics Data API).
/// One row per Date. Idempotent on Date.
/// </summary>
public class AnalyticsDailyTotal : BaseEntity
{
    public DateOnly Date { get; set; }
    public int Sessions { get; set; }
    public int Users { get; set; }
    public int NewUsers { get; set; }
    public int EngagedSessions { get; set; }
    public int Conversions { get; set; }
    public double EngagementRate { get; set; }
    public double AverageSessionDuration { get; set; }
    public int ScreenPageViews { get; set; }
}

/// <summary>
/// Per-day, per-source/medium GA4 metrics. Tells us where traffic comes from
/// (google/organic, direct/none, instagram.com/referral, etc.).
/// Unique on (Date, Source, Medium).
/// </summary>
public class AnalyticsDailySource : BaseEntity
{
    public DateOnly Date { get; set; }
    public required string Source { get; set; }
    public required string Medium { get; set; }
    public int Sessions { get; set; }
    public int Users { get; set; }
    public int Conversions { get; set; }
}

/// <summary>
/// Per-day, per-landing-page GA4 metrics. Shows which pages bring people
/// onto the site and how engaged they are once there.
/// Unique on (Date, LandingPage).
/// </summary>
public class AnalyticsDailyLandingPage : BaseEntity
{
    public DateOnly Date { get; set; }
    public required string LandingPage { get; set; }
    public int Sessions { get; set; }
    public int EngagedSessions { get; set; }
    public int Conversions { get; set; }
    public double AverageSessionDuration { get; set; }
}
