using EdenRelics.SellerTool.Data;
using Microsoft.EntityFrameworkCore;

namespace EdenRelics.SellerTool.Api;

/// <summary>
/// Writes events into the archive database. Deliberately does NOT save: it enlists the event in the
/// caller's existing unit of work so the event and the thing it describes commit together. A garment
/// that exists without its GarmentCreated event, or a flag raised with no record of it, would be a
/// silent hole in exactly the numbers the gate is judged on.
/// </summary>
public interface IEventRecorder
{
    /// <summary>Queue an event. Persisted by the caller's next SaveChanges.</summary>
    void Record(
        Guid sellerId,
        ToolEventKind kind,
        Guid? garmentId = null,
        string? platform = null,
        int? durationMs = null,
        string? detail = null,
        DateTime? occurredAtUtc = null);
}

public class EventRecorder(ToolDbContext db) : IEventRecorder
{
    /// <summary>
    /// How far back a client may date an event. The extension buffers while the seller is offline, so
    /// backdating is legitimate — but only within reason, and never into the future, or one client
    /// with a wrong clock quietly distorts every rate on the dashboard.
    /// </summary>
    public static readonly TimeSpan MaximumBackdating = TimeSpan.FromDays(30);

    public void Record(
        Guid sellerId,
        ToolEventKind kind,
        Guid? garmentId = null,
        string? platform = null,
        int? durationMs = null,
        string? detail = null,
        DateTime? occurredAtUtc = null)
    {
        DateTime now = DateTime.UtcNow;
        db.ToolEvents.Add(new ToolEvent
        {
            SellerId = sellerId,
            Kind = kind,
            GarmentId = garmentId,
            Platform = Trim(platform, 32),
            DurationMs = durationMs is > 0 ? durationMs : null,
            Detail = Trim(detail, 120),
            OccurredAtUtc = Clamp(occurredAtUtc, now),
        });
    }

    private static DateTime Clamp(DateTime? supplied, DateTime now)
    {
        if (supplied is not { } t)
        {
            return now;
        }
        DateTime utc = t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime();
        if (utc > now)
        {
            return now;
        }
        return utc < now - MaximumBackdating ? now - MaximumBackdating : utc;
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
        : value.Length <= max ? value.Trim()
        : value.Trim()[..max];
}

/// <summary>
/// The brief's §10 numbers, computed over a window. Rates are null rather than zero when nothing has
/// happened yet — "no measurements have been proposed" and "no measurement was ever accepted" are
/// opposite readings, and a dashboard that shows 0% for both is worse than one that shows neither.
/// </summary>
public interface IToolMetrics
{
    Task<MetricsSummaryDto> SummariseAsync(int days, CancellationToken ct = default);
}

public class ToolMetrics(ToolDbContext db) : IToolMetrics
{
    public async Task<MetricsSummaryDto> SummariseAsync(int days, CancellationToken ct = default)
    {
        int window = Math.Clamp(days, 1, 365);
        DateTime now = DateTime.UtcNow;
        DateTime from = now.AddDays(-window);
        DateTime weekAgo = now.AddDays(-7);

        List<ToolEvent> events = await db.ToolEvents
            .Where(e => e.OccurredAtUtc >= from)
            .ToListAsync(ct);

        int Count(ToolEventKind kind) => events.Count(e => e.Kind == kind);

        // The gate's first condition, and the only one this service can answer on its own: ten or more
        // sellers using the tool WEEKLY. Counted over seven days regardless of the window asked for,
        // because widening the window would flatter it.
        int weeklyActive = events
            .Where(e => e.OccurredAtUtc >= weekAgo)
            .Select(e => e.SellerId)
            .Distinct()
            .Count();

        int accepted = Count(ToolEventKind.MeasurementAccepted);
        int adjusted = Count(ToolEventKind.MeasurementAdjusted);
        int rejected = Count(ToolEventKind.MeasurementRejected);
        MeasurementMetricsDto measurement = new(
            Proposed: Count(ToolEventKind.MeasurementProposed),
            Accepted: accepted,
            Adjusted: adjusted,
            Rejected: rejected,
            // Adjusted counts against acceptance. A number the seller had to correct did not save them
            // the tape measure, which is the only thing the measurement tool promises.
            AcceptanceRate: Rate(accepted, accepted + adjusted + rejected));

        List<PlatformMetricsDto> platforms = events
            .Where(e => e.Platform is not null && e.Kind is ToolEventKind.ExtensionPublishAttempted
                or ToolEventKind.ExtensionPublishSucceeded or ToolEventKind.ExtensionPublishFailed)
            .GroupBy(e => e.Platform!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                int attempted = g.Count(e => e.Kind == ToolEventKind.ExtensionPublishAttempted);
                int failed = g.Count(e => e.Kind == ToolEventKind.ExtensionPublishFailed);
                return new PlatformMetricsDto(
                    g.Key,
                    attempted,
                    g.Count(e => e.Kind == ToolEventKind.ExtensionPublishSucceeded),
                    failed,
                    Rate(failed, attempted),
                    // The reasons, commonest first — a failure rate tells you the extension is broken,
                    // this tells you what to fix after a marketplace redesign.
                    [.. g.Where(e => e.Kind == ToolEventKind.ExtensionPublishFailed && e.Detail is not null)
                        .GroupBy(e => e.Detail!)
                        .OrderByDescending(r => r.Count())
                        .Take(5)
                        .Select(r => new FailureReasonDto(r.Key, r.Count()))]);
            })
            .ToList();

        int raised = Count(ToolEventKind.DatingFlagRaised);
        int upheld = Count(ToolEventKind.DatingFlagUpheld);
        int dismissed = Count(ToolEventKind.DatingFlagDismissed);
        DatingFlagMetricsDto flags = new(
            Raised: raised,
            Upheld: upheld,
            Dismissed: dismissed,
            Unresolved: Math.Max(0, raised - upheld - dismissed),
            // The number the whole thesis rests on: of the flags a seller actually ruled on, how often
            // was the tool right. Measured against resolved flags only — counting unresolved ones as
            // wrong would make an unused feature look like a failing one.
            UpheldRate: Rate(upheld, upheld + dismissed));

        List<int> durations = [.. events
            .Where(e => e.Kind == ToolEventKind.ListingPublished && e.DurationMs is > 0)
            .Select(e => e.DurationMs!.Value)
            .OrderBy(d => d)];

        return new MetricsSummaryDto(
            FromUtc: from,
            ToUtc: now,
            Days: window,
            WeeklyActiveSellers: weeklyActive,
            ActiveSellersInWindow: events.Select(e => e.SellerId).Distinct().Count(),
            GarmentsCreated: Count(ToolEventKind.GarmentCreated),
            ListingsPublished: Count(ToolEventKind.ListingPublished),
            // Median, not mean: one listing abandoned over lunch and resumed after would drag an
            // average far enough to make the headline saving meaningless.
            MedianSecondsPerListing: Median(durations) is { } ms ? (int)Math.Round(ms / 1000.0) : null,
            Measurement: measurement,
            Extension: platforms,
            DatingFlags: flags);
    }

    private static double? Rate(int numerator, int denominator) =>
        denominator == 0 ? null : Math.Round((double)numerator / denominator, 4);

    private static double? Median(List<int> sorted)
    {
        if (sorted.Count == 0)
        {
            return null;
        }
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
