using EdenRelics.SellerTool.Data;
using Microsoft.EntityFrameworkCore;

namespace EdenRelics.SellerTool.Api.Tests;

/// <summary>
/// The §10 numbers are what the go/no-go gate is decided on, so the arithmetic is tested directly
/// rather than inferred from the endpoint. Most of these assert a distinction that a naive count would
/// get wrong — an adjusted measurement is not an accepted one, an unresolved flag is not a wrong one,
/// and no data at all is not the same as a zero.
/// </summary>
public class InstrumentationTests
{
    private static ToolDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ToolDbContext>()
            .UseInMemoryDatabase($"events-{Guid.NewGuid():N}")
            .Options);

    private static void Seed(
        ToolDbContext db, ToolEventKind kind, Guid seller, double daysAgo = 0,
        string? platform = null, int? durationMs = null, string? detail = null)
    {
        db.ToolEvents.Add(new ToolEvent
        {
            SellerId = seller,
            Kind = kind,
            Platform = platform,
            DurationMs = durationMs,
            Detail = detail,
            OccurredAtUtc = DateTime.UtcNow.AddDays(-daysAgo),
        });
    }

    // ---- The recorder ----

    [Fact]
    public async Task Record_DoesNotSave_SoTheEventCommitsWithTheThingItDescribes()
    {
        await using ToolDbContext db = NewDb();
        EventRecorder recorder = new(db);

        recorder.Record(Guid.NewGuid(), ToolEventKind.ListingPublished);

        Assert.Empty(await db.ToolEvents.ToListAsync());   // queued, not written

        await db.SaveChangesAsync();
        Assert.Single(await db.ToolEvents.ToListAsync());
    }

    [Fact]
    public async Task Record_RejectsAFutureTimestamp_SoAWrongClientClockCannotDistortRates()
    {
        await using ToolDbContext db = NewDb();
        EventRecorder recorder = new(db);
        DateTime before = DateTime.UtcNow;

        recorder.Record(Guid.NewGuid(), ToolEventKind.ListingPublished,
            occurredAtUtc: DateTime.UtcNow.AddDays(3));
        await db.SaveChangesAsync();

        ToolEvent stored = await db.ToolEvents.SingleAsync();
        Assert.InRange(stored.OccurredAtUtc, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task Record_AllowsBackdating_UpToTheBufferingLimit()
    {
        await using ToolDbContext db = NewDb();
        EventRecorder recorder = new(db);

        // An extension that was offline for two days: legitimate, and kept as reported.
        recorder.Record(Guid.NewGuid(), ToolEventKind.ExtensionPublishFailed,
            platform: "Vinted", occurredAtUtc: DateTime.UtcNow.AddDays(-2));
        // Implausible: clamped to the floor rather than silently accepted into an old window.
        recorder.Record(Guid.NewGuid(), ToolEventKind.ExtensionPublishFailed,
            platform: "Vinted", occurredAtUtc: DateTime.UtcNow.AddDays(-400));
        await db.SaveChangesAsync();

        List<ToolEvent> stored = await db.ToolEvents.OrderByDescending(e => e.OccurredAtUtc).ToListAsync();
        DateTime now = DateTime.UtcNow;
        // Kept as reported.
        Assert.InRange(stored[0].OccurredAtUtc, now.AddDays(-2).AddMinutes(-1), now.AddDays(-2).AddMinutes(1));
        // Pulled forward to the floor, not left sitting 400 days back.
        Assert.InRange(
            stored[1].OccurredAtUtc,
            now - EventRecorder.MaximumBackdating.Add(TimeSpan.FromMinutes(1)),
            now - EventRecorder.MaximumBackdating.Add(TimeSpan.FromMinutes(-1)));
    }

    // ---- The summary ----

    [Fact]
    public async Task Summary_ReportsNullRates_WhenNothingHasHappenedYet()
    {
        await using ToolDbContext db = NewDb();

        MetricsSummaryDto summary = await new ToolMetrics(db).SummariseAsync(28);

        // Null, not zero: "nobody has measured anything" and "every measurement was rejected" are
        // opposite readings and must not render identically.
        Assert.Null(summary.Measurement.AcceptanceRate);
        Assert.Null(summary.DatingFlags.UpheldRate);
        Assert.Null(summary.MedianSecondsPerListing);
        Assert.Empty(summary.Extension);
    }

    [Fact]
    public async Task Summary_CountsAnAdjustedMeasurementAgainstAcceptance()
    {
        await using ToolDbContext db = NewDb();
        Guid seller = Guid.NewGuid();
        Seed(db, ToolEventKind.MeasurementAccepted, seller);
        Seed(db, ToolEventKind.MeasurementAccepted, seller);
        Seed(db, ToolEventKind.MeasurementAdjusted, seller);   // the seller had to drag a point
        Seed(db, ToolEventKind.MeasurementRejected, seller);
        await db.SaveChangesAsync();

        MetricsSummaryDto summary = await new ToolMetrics(db).SummariseAsync(28);

        // 2 of 4, not 3 of 4: a number the seller had to correct did not save them the tape measure.
        Assert.Equal(0.5, summary.Measurement.AcceptanceRate);
    }

    [Fact]
    public async Task Summary_MeasuresFlagsAgainstResolvedOnes_SoAnUnusedFeatureIsNotAFailingOne()
    {
        await using ToolDbContext db = NewDb();
        Guid seller = Guid.NewGuid();
        for (int i = 0; i < 10; i++)
        {
            Seed(db, ToolEventKind.DatingFlagRaised, seller);
        }
        Seed(db, ToolEventKind.DatingFlagUpheld, seller);
        Seed(db, ToolEventKind.DatingFlagUpheld, seller);
        Seed(db, ToolEventKind.DatingFlagUpheld, seller);
        Seed(db, ToolEventKind.DatingFlagDismissed, seller);
        await db.SaveChangesAsync();

        MetricsSummaryDto summary = await new ToolMetrics(db).SummariseAsync(28);

        Assert.Equal(10, summary.DatingFlags.Raised);
        Assert.Equal(6, summary.DatingFlags.Unresolved);
        // 3 of the 4 the seller actually ruled on — not 3 of 10.
        Assert.Equal(0.75, summary.DatingFlags.UpheldRate);
    }

    [Fact]
    public async Task Summary_UsesAMedianForTimePerListing_SoOneAbandonedDraftDoesNotDominate()
    {
        await using ToolDbContext db = NewDb();
        Guid seller = Guid.NewGuid();
        foreach (int ms in new[] { 60_000, 90_000, 120_000, 7_200_000 })   // …and one left open over lunch
        {
            Seed(db, ToolEventKind.ListingPublished, seller, durationMs: ms);
        }
        await db.SaveChangesAsync();

        MetricsSummaryDto summary = await new ToolMetrics(db).SummariseAsync(28);

        // Median of 90s and 120s = 105s. The mean would be over 30 minutes and would say nothing true.
        Assert.Equal(105, summary.MedianSecondsPerListing);
        Assert.Equal(4, summary.ListingsPublished);
    }

    [Fact]
    public async Task Summary_ReportsExtensionFailuresPerPlatform_WithTheCommonestReasons()
    {
        await using ToolDbContext db = NewDb();
        Guid seller = Guid.NewGuid();
        for (int i = 0; i < 4; i++)
        {
            Seed(db, ToolEventKind.ExtensionPublishAttempted, seller, platform: "Vinted");
        }
        Seed(db, ToolEventKind.ExtensionPublishSucceeded, seller, platform: "Vinted");
        Seed(db, ToolEventKind.ExtensionPublishFailed, seller, platform: "Vinted", detail: "selector-missing");
        Seed(db, ToolEventKind.ExtensionPublishFailed, seller, platform: "Vinted", detail: "selector-missing");
        Seed(db, ToolEventKind.ExtensionPublishFailed, seller, platform: "Vinted", detail: "session-expired");
        Seed(db, ToolEventKind.ExtensionPublishAttempted, seller, platform: "Depop");
        await db.SaveChangesAsync();

        MetricsSummaryDto summary = await new ToolMetrics(db).SummariseAsync(28);

        PlatformMetricsDto vinted = summary.Extension.Single(p => p.Platform == "Vinted");
        Assert.Equal(4, vinted.Attempted);
        Assert.Equal(3, vinted.Failed);
        Assert.Equal(0.75, vinted.FailureRate);
        // What to fix first after a marketplace redesign.
        Assert.Equal("selector-missing", vinted.TopReasons[0].Reason);
        Assert.Equal(2, vinted.TopReasons[0].Count);

        // One attempt, no failure: a real 0%, distinct from a platform nobody has tried at all.
        PlatformMetricsDto depop = summary.Extension.Single(p => p.Platform == "Depop");
        Assert.Equal(0d, depop.FailureRate);
        Assert.Empty(depop.TopReasons);
    }

    [Fact]
    public async Task Summary_CountsWeeklyActiveSellersOverSevenDays_WhateverWindowIsAskedFor()
    {
        await using ToolDbContext db = NewDb();
        Guid recent1 = Guid.NewGuid(), recent2 = Guid.NewGuid(), lapsed = Guid.NewGuid();
        Seed(db, ToolEventKind.GarmentCreated, recent1, daysAgo: 1);
        Seed(db, ToolEventKind.GarmentCreated, recent1, daysAgo: 2);   // same seller, still one
        Seed(db, ToolEventKind.GarmentCreated, recent2, daysAgo: 6);
        Seed(db, ToolEventKind.GarmentCreated, lapsed, daysAgo: 20);   // inside the window, not the week
        await db.SaveChangesAsync();

        MetricsSummaryDto summary = await new ToolMetrics(db).SummariseAsync(90);

        // The gate says WEEKLY. Widening the reporting window must not flatter it.
        Assert.Equal(2, summary.WeeklyActiveSellers);
        Assert.Equal(3, summary.ActiveSellersInWindow);
    }

    [Fact]
    public async Task Summary_ExcludesEventsOlderThanTheWindow()
    {
        await using ToolDbContext db = NewDb();
        Guid seller = Guid.NewGuid();
        Seed(db, ToolEventKind.GarmentCreated, seller, daysAgo: 3);
        Seed(db, ToolEventKind.GarmentCreated, seller, daysAgo: 40);
        await db.SaveChangesAsync();

        MetricsSummaryDto summary = await new ToolMetrics(db).SummariseAsync(28);

        Assert.Equal(1, summary.GarmentsCreated);
    }
}
