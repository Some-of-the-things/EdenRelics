using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Eden_Relics_BE.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// The engine is pure over its inputs, so these drive it directly rather than through the
/// API. Repositories are only needed by the persisting overload and are never touched by
/// <see cref="IDatingRulesEngine.Assess"/>.
/// </summary>
public class DatingRulesEngineTests
{
    private static DatingRulesEngine Engine(DatingOptions? options = null) =>
        new(
            garments: null!,
            evidenceRepo: null!,
            rules: null!,
            assessments: null!,
            Options.Create(options ?? new DatingOptions()),
            NullLogger<DatingRulesEngine>.Instance);

    private static GarmentEvidence Ev(
        EvidenceType type,
        string value,
        ConfirmationState confirmation = ConfirmationState.HumanConfirmed) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Value = value,
            Confirmation = confirmation,
        };

    /// <summary>The shipped seed rule with this code, asserted against as real content.</summary>
    private static DatingRule SeedRule(string code) =>
        DatingRulesSeed.BuildSeed().Single(r => r.Code == code);

    private static DatingRule Rule(
        string code,
        EvidenceType type,
        string testValue,
        DateBoundType bound,
        DateOnly date,
        RuleStrength strength = RuleStrength.Hard,
        RuleStatus status = RuleStatus.Active,
        int? tolerance = 0) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Description = code,
            EvidenceType = type,
            TestKind = EvidenceTestKind.ValueContains,
            TestValue = testValue,
            BoundType = bound,
            BoundDate = date,
            Strength = strength,
            Status = status,
            TrailingToleranceMonths = tolerance,
        };

    /// <summary>
    /// The worked example from the engineering brief §3.2, and the acceptance criterion for
    /// the whole engine: a cut-label dress dated to 1980-1986 from three independent
    /// observations, using no brand knowledge whatsoever.
    ///
    /// Tolerance is pinned to 0 here so the assertion is the strict intersection the brief
    /// states. Tolerance behaviour is covered separately below.
    /// </summary>
    [Fact]
    public void WorkedExample_CutLabelDress_NarrowsTo1980To1986()
    {
        List<GarmentEvidence> evidence =
        [
            Ev(EvidenceType.CareLabel, "tumble_dry_symbol"),
            Ev(EvidenceType.CareLabel, "numbered_wash_tub"),
            Ev(EvidenceType.PhoneNumber, "01-629-1234"),
        ];

        List<DatingRule> rules =
        [
            Rule("TUMBLE", EvidenceType.CareLabel, "tumble_dry_symbol",
                DateBoundType.NotBefore, new DateOnly(1980, 1, 1)),
            Rule("WASHTUB", EvidenceType.CareLabel, "numbered_wash_tub",
                DateBoundType.NotAfter, new DateOnly(1986, 12, 31)),
            Rule("PHONE01", EvidenceType.PhoneNumber, "01-",
                DateBoundType.NotAfter, new DateOnly(1990, 5, 6)),
        ];

        DatingAssessment result = Engine().Assess(evidence, rules);

        Assert.Equal(DatingOutcome.Bounded, result.Outcome);
        Assert.Equal(new DateOnly(1980, 1, 1), result.Earliest);
        Assert.Equal(new DateOnly(1986, 12, 31), result.Latest);
        Assert.False(result.HasHardContradiction);
        // No brand label anywhere in the evidence set, and that is a complete answer.
        Assert.Contains("maker unknown", result.Summary);
        Assert.Contains("1980-1986", result.Summary);
    }

    [Fact]
    public void UnverifiedRules_NeverAffectOutput()
    {
        List<GarmentEvidence> evidence = [Ev(EvidenceType.OriginText, "Empire Made")];
        List<DatingRule> rules =
        [
            Rule("EMPIRE", EvidenceType.OriginText, "empire made",
                DateBoundType.NotAfter, new DateOnly(1965, 12, 31), status: RuleStatus.Unverified),
        ];

        DatingAssessment result = Engine().Assess(evidence, rules);

        Assert.Equal(DatingOutcome.Unbounded, result.Outcome);
        Assert.Empty(result.Steps);
        Assert.Null(result.Latest);
    }

    [Fact]
    public void RetiredRules_NeverAffectOutput()
    {
        List<GarmentEvidence> evidence = [Ev(EvidenceType.CareLabel, "tumble_dry_symbol")];
        List<DatingRule> rules =
        [
            Rule("TUMBLE", EvidenceType.CareLabel, "tumble_dry_symbol",
                DateBoundType.NotBefore, new DateOnly(1980, 1, 1), status: RuleStatus.Retired),
        ];

        Assert.Equal(DatingOutcome.Unbounded, Engine().Assess(evidence, rules).Outcome);
    }

    /// <summary>
    /// An impossible combination is surfaced, not silently reconciled into a midpoint.
    /// </summary>
    [Fact]
    public void HardContradiction_IsSurfacedNotAveraged()
    {
        List<GarmentEvidence> evidence =
        [
            Ev(EvidenceType.CareLabel, "tumble_dry_symbol"),
            Ev(EvidenceType.OriginText, "Made in West Germany"),
        ];

        List<DatingRule> rules =
        [
            Rule("TUMBLE", EvidenceType.CareLabel, "tumble_dry_symbol",
                DateBoundType.NotBefore, new DateOnly(1980, 1, 1)),
            // Deliberately impossible against the above.
            Rule("IMPOSSIBLE", EvidenceType.OriginText, "west germany",
                DateBoundType.NotAfter, new DateOnly(1975, 1, 1)),
        ];

        DatingAssessment result = Engine().Assess(evidence, rules);

        Assert.Equal(DatingOutcome.Contradiction, result.Outcome);
        Assert.True(result.HasHardContradiction);
        Assert.Contains("Contradiction", result.Summary);
    }

    /// <summary>
    /// Absence evidence must never be able to block a listing on its own — it is exactly
    /// the case where a confident system would be wrong.
    /// </summary>
    [Fact]
    public void SoftBound_ConflictingWithHardEvidence_LowersConfidenceButStillDates()
    {
        List<GarmentEvidence> evidence =
        [
            Ev(EvidenceType.CareLabel, "tumble_dry_symbol"),
            Ev(EvidenceType.CareLabel, "absent"),
        ];

        List<DatingRule> rules =
        [
            Rule("TUMBLE", EvidenceType.CareLabel, "tumble_dry_symbol",
                DateBoundType.NotBefore, new DateOnly(1980, 1, 1)),
            Rule("NO_CARE_LABEL", EvidenceType.CareLabel, "absent",
                DateBoundType.NotAfter, new DateOnly(1966, 1, 1), strength: RuleStrength.Soft),
        ];

        DatingAssessment result = Engine().Assess(evidence, rules);

        Assert.Equal(DatingOutcome.Bounded, result.Outcome);
        Assert.False(result.HasHardContradiction);
        Assert.True(result.HasSoftContradiction);
        Assert.Equal(new DateOnly(1980, 1, 1), result.Earliest);

        DatingAssessmentStep softStep = result.Steps.Single(s => s.RuleCode == "NO_CARE_LABEL");
        Assert.False(softStep.AppliedToInterval);
        Assert.NotNull(softStep.ExclusionReason);
    }

    /// <summary>
    /// Trailing edges widen because old label stock outlives the change it records; leading
    /// edges never do, because a garment cannot carry a format that did not yet exist.
    /// </summary>
    [Fact]
    public void Tolerance_WidensTrailingEdgeOnly()
    {
        List<GarmentEvidence> evidence =
        [
            Ev(EvidenceType.CareLabel, "tumble_dry_symbol"),
            Ev(EvidenceType.PhoneNumber, "01-629-1234"),
        ];

        List<DatingRule> rules =
        [
            // Null tolerance → falls back to the configured default.
            Rule("TUMBLE", EvidenceType.CareLabel, "tumble_dry_symbol",
                DateBoundType.NotBefore, new DateOnly(1980, 1, 1), tolerance: null),
            Rule("PHONE01", EvidenceType.PhoneNumber, "01-",
                DateBoundType.NotAfter, new DateOnly(1990, 5, 6), tolerance: null),
        ];

        DatingAssessment result = Engine(new DatingOptions { DefaultTrailingToleranceMonths = 12 })
            .Assess(evidence, rules);

        Assert.Equal(new DateOnly(1980, 1, 1), result.Earliest);   // leading edge untouched
        Assert.Equal(new DateOnly(1991, 5, 6), result.Latest);     // trailing edge +12 months

        DatingAssessmentStep leading = result.Steps.Single(s => s.RuleCode == "TUMBLE");
        Assert.Equal(0, leading.ToleranceMonthsApplied);
        DatingAssessmentStep trailing = result.Steps.Single(s => s.RuleCode == "PHONE01");
        Assert.Equal(12, trailing.ToleranceMonthsApplied);
        // The raw bound stays visible next to the adjusted one.
        Assert.Equal(new DateOnly(1990, 5, 6), trailing.BoundDate);
    }

    [Fact]
    public void ClaimedEra_OutsideWindow_IsFlagged()
    {
        List<GarmentEvidence> evidence = [Ev(EvidenceType.CareLabel, "tumble_dry_symbol")];
        List<DatingRule> rules =
        [
            Rule("TUMBLE", EvidenceType.CareLabel, "tumble_dry_symbol",
                DateBoundType.NotBefore, new DateOnly(1980, 1, 1)),
        ];

        // The seller says 1970s; the tumble-dry symbol says not before 1980.
        DatingAssessment flagged = Engine().Assess(
            evidence, rules, new DateOnly(1970, 1, 1), new DateOnly(1979, 12, 31));
        Assert.True(flagged.ContradictsClaimedEra);

        DatingAssessment fine = Engine().Assess(
            evidence, rules, new DateOnly(1982, 1, 1), new DateOnly(1986, 12, 31));
        Assert.False(fine.ContradictsClaimedEra);
    }

    [Fact]
    public void RejectedEvidence_IsIgnored()
    {
        List<GarmentEvidence> evidence =
        [
            Ev(EvidenceType.CareLabel, "tumble_dry_symbol", ConfirmationState.Rejected),
        ];
        List<DatingRule> rules =
        [
            Rule("TUMBLE", EvidenceType.CareLabel, "tumble_dry_symbol",
                DateBoundType.NotBefore, new DateOnly(1980, 1, 1)),
        ];

        Assert.Equal(DatingOutcome.Unbounded, Engine().Assess(evidence, rules).Outcome);
    }

    /// <summary>
    /// A conclusion drawn from a machine reading is itself only a proposal, however sound
    /// the rules are. Confirmation has to propagate or the archive fills with guesses that
    /// look like ground truth.
    /// </summary>
    [Fact]
    public void ProposedEvidence_KeepsTheAssessmentProposed()
    {
        List<DatingRule> rules =
        [
            Rule("TUMBLE", EvidenceType.CareLabel, "tumble_dry_symbol",
                DateBoundType.NotBefore, new DateOnly(1980, 1, 1)),
        ];

        DatingAssessment proposed = Engine().Assess(
            [Ev(EvidenceType.CareLabel, "tumble_dry_symbol", ConfirmationState.Proposed)], rules);
        Assert.Equal(ConfirmationState.Proposed, proposed.Confirmation);

        DatingAssessment confirmed = Engine().Assess(
            [Ev(EvidenceType.CareLabel, "tumble_dry_symbol", ConfirmationState.HumanConfirmed)], rules);
        Assert.Equal(ConfirmationState.HumanConfirmed, confirmed.Confirmation);
    }

    [Fact]
    public void OneSidedWindow_IsAValidAnswer()
    {
        List<GarmentEvidence> evidence = [Ev(EvidenceType.CareLabel, "tumble_dry_symbol")];
        List<DatingRule> rules =
        [
            Rule("TUMBLE", EvidenceType.CareLabel, "tumble_dry_symbol",
                DateBoundType.NotBefore, new DateOnly(1980, 1, 1)),
        ];

        DatingAssessment result = Engine().Assess(evidence, rules);

        Assert.Equal(DatingOutcome.Bounded, result.Outcome);
        Assert.Equal(new DateOnly(1980, 1, 1), result.Earliest);
        Assert.Null(result.Latest);
        Assert.Contains("1980 or later", result.Summary);
    }

    [Fact]
    public void BrandLabelPresent_DropsTheMakerUnknownQualifier()
    {
        List<GarmentEvidence> evidence =
        [
            Ev(EvidenceType.CareLabel, "tumble_dry_symbol"),
            Ev(EvidenceType.BrandLabel, "Laura Ashley"),
        ];
        List<DatingRule> rules =
        [
            Rule("TUMBLE", EvidenceType.CareLabel, "tumble_dry_symbol",
                DateBoundType.NotBefore, new DateOnly(1980, 1, 1)),
        ];

        Assert.DoesNotContain("maker unknown", Engine().Assess(evidence, rules).Summary);
    }

    /// <summary>
    /// Rule content is authored separately from the code that runs it, so an unusable
    /// pattern must fail closed — a rule that cannot be evaluated must not bound a date.
    /// </summary>
    [Fact]
    public void InvalidRegexRule_FailsClosed()
    {
        List<GarmentEvidence> evidence = [Ev(EvidenceType.PhoneNumber, "01-629-1234")];
        List<DatingRule> rules =
        [
            new()
            {
                Id = Guid.CreateVersion7(),
                Code = "BAD_REGEX",
                Description = "unparseable",
                EvidenceType = EvidenceType.PhoneNumber,
                TestKind = EvidenceTestKind.ValueMatchesRegex,
                TestValue = "([unclosed",
                BoundType = DateBoundType.NotAfter,
                BoundDate = new DateOnly(1990, 5, 6),
                Status = RuleStatus.Active,
            },
        ];

        Assert.Equal(DatingOutcome.Unbounded, Engine().Assess(evidence, rules).Outcome);
    }

    /// <summary>The seeded telephone rules have to actually match real label formats.</summary>
    [Theory]
    [InlineData("01-629 1234", true)]
    [InlineData("01 629 1234", true)]
    [InlineData("071-629 1234", false)]
    [InlineData("0161 236 1234", false)]  // post-1995 provincial 01x1, not a bare London 01
    public void SeededBareLondon01Rule_MatchesOnlyPre1990Formats(string number, bool shouldBound)
    {
        DatingRule rule = SeedRule("PHONE_UK_BARE_01_LONDON");
        DatingAssessment result = Engine().Assess([Ev(EvidenceType.PhoneNumber, number)], [rule]);

        Assert.Equal(shouldBound ? DatingOutcome.Bounded : DatingOutcome.Unbounded, result.Outcome);
    }

    [Fact]
    public void Seed_MarksUnsourcedRulesUnverified()
    {
        // "Empire Made" is trade practice rather than legislation and has no primary source
        // yet, so it must ship inert.
        Assert.Equal(RuleStatus.Unverified, SeedRule("ORIGIN_BRITISH_EMPIRE_MADE").Status);
        Assert.Equal(RuleStatus.Active, SeedRule("PHONE_UK_BARE_01_LONDON").Status);
    }
}
