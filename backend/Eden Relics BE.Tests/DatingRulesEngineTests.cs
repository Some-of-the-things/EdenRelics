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
            transitionGroups: null!,
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

    // ── Transition groups (rules doc §0.4) ───────────────────────────────────

    private static DatingTransitionGroup Group(
        string code,
        DateOnly start,
        DateOnly end,
        RuleStatus status = RuleStatus.Active,
        int? tolerance = 0) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Description = code,
            PeriodStart = start,
            PeriodEnd = end,
            Status = status,
            TrailingToleranceMonths = tolerance,
        };

    /// <summary>
    /// The case the group exists for. A numbered wash tub caps at 1986 and an agitation
    /// underline floors at 1986, so intersecting them collapses the answer onto the
    /// changeover itself. Both conventions are documented as coexisting across 1980-1986,
    /// so the honest answer is the whole transition, not the instant the standard changed.
    /// </summary>
    [Fact]
    public void TransitionGroup_CoOccurringRules_WidenToThePeriodInsteadOfCollapsing()
    {
        List<GarmentEvidence> evidence =
        [
            Ev(EvidenceType.CareLabel, "numbered_wash_tub"),
            Ev(EvidenceType.CareLabel, "wash_tub_underline"),
        ];

        List<DatingRule> rules =
        [
            Rule("TUB", EvidenceType.CareLabel, "numbered_wash_tub",
                DateBoundType.NotAfter, new DateOnly(1986, 12, 31)),
            Rule("UNDERLINE", EvidenceType.CareLabel, "wash_tub_underline",
                DateBoundType.NotBefore, new DateOnly(1986, 1, 1)),
        ];
        rules[0].TransitionGroupCode = "CARE-1986";
        rules[1].TransitionGroupCode = "CARE-1986";

        DatingAssessment result = Engine().Assess(
            evidence, rules, transitionGroups: [Group("CARE-1986", new DateOnly(1980, 1, 1), new DateOnly(1986, 12, 31))]);

        Assert.Equal(DatingOutcome.Bounded, result.Outcome);
        Assert.Equal(new DateOnly(1980, 1, 1), result.Earliest);
        Assert.Equal(new DateOnly(1986, 12, 31), result.Latest);
        Assert.False(result.HasHardContradiction);

        // The member rules stay in the chain, set aside with a reason naming the group —
        // "we saw this and widened because of it" is the audit trail.
        DatingAssessmentStep tub = result.Steps.Single(s => s.RuleCode == "TUB");
        Assert.False(tub.AppliedToInterval);
        Assert.Contains("CARE-1986", tub.ExclusionReason);
        Assert.Equal(2, result.Steps.Count(s => s.TransitionGroupCode == "CARE-1986" && s.AppliedToInterval));
    }

    /// <summary>
    /// One convention on its own is not a co-occurrence, so it must still bound normally.
    /// If a lone member rule triggered the group, every numbered tub would widen to the
    /// whole transition and the rule would stop being worth anything.
    /// </summary>
    [Fact]
    public void TransitionGroup_SingleMemberRule_BoundsNormally()
    {
        List<DatingRule> rules =
        [
            Rule("TUB", EvidenceType.CareLabel, "numbered_wash_tub",
                DateBoundType.NotAfter, new DateOnly(1986, 12, 31)),
        ];
        rules[0].TransitionGroupCode = "CARE-1986";

        DatingAssessment result = Engine().Assess(
            [Ev(EvidenceType.CareLabel, "numbered_wash_tub")],
            rules,
            transitionGroups: [Group("CARE-1986", new DateOnly(1980, 1, 1), new DateOnly(1986, 12, 31))]);

        Assert.Equal(new DateOnly(1986, 12, 31), result.Latest);
        // Not floored at the group's 1980 start: nothing here says the garment is post-1980.
        Assert.Null(result.Earliest);
        Assert.True(result.Steps.Single().AppliedToInterval);
    }

    [Fact]
    public void TransitionGroup_Unverified_IsInertLikeAnUnverifiedRule()
    {
        List<DatingRule> rules =
        [
            Rule("TUB", EvidenceType.CareLabel, "numbered_wash_tub",
                DateBoundType.NotAfter, new DateOnly(1986, 12, 31)),
            Rule("UNDERLINE", EvidenceType.CareLabel, "wash_tub_underline",
                DateBoundType.NotBefore, new DateOnly(1986, 1, 1)),
        ];
        rules[0].TransitionGroupCode = "CARE-1986";
        rules[1].TransitionGroupCode = "CARE-1986";

        DatingAssessment result = Engine().Assess(
            [Ev(EvidenceType.CareLabel, "numbered_wash_tub"), Ev(EvidenceType.CareLabel, "wash_tub_underline")],
            rules,
            transitionGroups:
            [
                Group("CARE-1986", new DateOnly(1980, 1, 1), new DateOnly(1986, 12, 31), RuleStatus.Unverified),
            ]);

        // Falls back to plain intersection: the changeover instant, not the transition.
        Assert.Equal(new DateOnly(1986, 1, 1), result.Earliest);
        Assert.Equal(new DateOnly(1986, 12, 31), result.Latest);
    }

    /// <summary>
    /// The widened bound can be no stronger than the evidence that triggered it, or a group
    /// reached through soft rules would launder them into a hard window.
    /// </summary>
    [Fact]
    public void TransitionGroup_TriggeredBySoftRules_ProducesASoftWindow()
    {
        List<DatingRule> rules =
        [
            Rule("A", EvidenceType.CareLabel, "a", DateBoundType.NotAfter, new DateOnly(1986, 12, 31),
                strength: RuleStrength.Soft),
            Rule("B", EvidenceType.CareLabel, "b", DateBoundType.NotBefore, new DateOnly(1986, 1, 1),
                strength: RuleStrength.Hard),
        ];
        rules[0].TransitionGroupCode = "G";
        rules[1].TransitionGroupCode = "G";

        DatingAssessment result = Engine().Assess(
            [Ev(EvidenceType.CareLabel, "a"), Ev(EvidenceType.CareLabel, "b")],
            rules,
            transitionGroups: [Group("G", new DateOnly(1980, 1, 1), new DateOnly(1986, 12, 31))]);

        Assert.All(
            result.Steps.Where(s => s.TransitionGroupCode == "G"),
            s => Assert.Equal(RuleStrength.Soft, s.Strength));
        Assert.Equal(new DateOnly(1980, 1, 1), result.Earliest);
    }

    [Fact]
    public void TransitionGroup_AppliesTrailingToleranceToItsPeriodEnd()
    {
        List<DatingRule> rules =
        [
            Rule("A", EvidenceType.CareLabel, "a", DateBoundType.NotAfter, new DateOnly(1986, 12, 31)),
            Rule("B", EvidenceType.CareLabel, "b", DateBoundType.NotBefore, new DateOnly(1986, 1, 1)),
        ];
        rules[0].TransitionGroupCode = "G";
        rules[1].TransitionGroupCode = "G";

        DatingAssessment result = Engine().Assess(
            [Ev(EvidenceType.CareLabel, "a"), Ev(EvidenceType.CareLabel, "b")],
            rules,
            transitionGroups: [Group("G", new DateOnly(1980, 1, 1), new DateOnly(1986, 12, 31), tolerance: 12)]);

        // Leading edge untouched, trailing edge widened — the same asymmetry as a rule.
        Assert.Equal(new DateOnly(1980, 1, 1), result.Earliest);
        Assert.Equal(new DateOnly(1987, 12, 31), result.Latest);
    }

    // ── Provenance (rules doc §0.3) ──────────────────────────────────────────

    /// <summary>
    /// Provenance is copied onto the step, not looked up from the live rule. An assessment
    /// that said "post-1980 on primary legislation" must not silently become "on community
    /// consensus" because the rule was re-sourced afterwards.
    /// </summary>
    [Fact]
    public void Provenance_IsCopiedIntoTheEvidenceChain()
    {
        DatingRule rule = Rule("R", EvidenceType.Fabric, "lyocell",
            DateBoundType.NotBefore, new DateOnly(1998, 1, 1));
        rule.Provenance = ProvenanceClass.PrimaryLegislation;

        DatingAssessment result = Engine().Assess([Ev(EvidenceType.Fabric, "lyocell")], [rule]);

        Assert.Equal(ProvenanceClass.PrimaryLegislation, result.Steps.Single().Provenance);
    }

    [Fact]
    public void Seed_EveryRuleCarriesItsSpecIdAndAProvenance()
    {
        List<DatingRule> seed = DatingRulesSeed.BuildSeed();

        Assert.NotEmpty(seed);
        Assert.All(seed, r => Assert.False(string.IsNullOrWhiteSpace(r.SpecId)));
        // Codes must stay unique — the seeder reconciles on Code, so a duplicate would make
        // one of the pair unreachable and silently un-updatable.
        Assert.Equal(seed.Count, seed.Select(r => r.Code).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The standing warning from §12 of the rules document, enforced. Anything still
    /// awaiting a source must be inert, and "PENDING" in a citation is how the seed marks
    /// that. An active rule with an unresolved source is the exact failure mode that flags
    /// correct listings as wrong.
    /// </summary>
    [Fact]
    public void Seed_NoActiveRuleHasAPendingCitation()
    {
        List<string> offenders = DatingRulesSeed.BuildSeed()
            .Where(r => r.Status == RuleStatus.Active
                && r.SourceCitation.Contains("PENDING", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Code)
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Rule content is authored separately from the code that runs it, and the engine
    /// deliberately fails a bad regex CLOSED — it logs and treats it as no match. That is
    /// right at runtime and useless as feedback, so an unparseable pattern would ship as a
    /// rule that silently never fires. Catch it here instead.
    /// </summary>
    [Fact]
    public void Seed_EveryRegexRuleHasAValidPattern()
    {
        foreach (DatingRule rule in DatingRulesSeed.BuildSeed()
            .Where(r => r.TestKind == EvidenceTestKind.ValueMatchesRegex))
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.TestValue), $"{rule.Code} has no pattern.");
            Exception? failure = Record.Exception(() =>
                System.Text.RegularExpressions.Regex.IsMatch("probe", rule.TestValue!));
            Assert.True(failure is null, $"{rule.Code} has an invalid regex: {failure?.Message}");
        }
    }

    [Fact]
    public void Seed_TransitionGroupMembersAllReferToAGroupThatExists()
    {
        HashSet<string> groups = DatingRulesSeed.BuildTransitionGroups()
            .Select(g => g.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<string> dangling = DatingRulesSeed.BuildSeed()
            .Where(r => r.TransitionGroupCode is not null && !groups.Contains(r.TransitionGroupCode))
            .Select(r => r.Code)
            .ToList();

        Assert.Empty(dangling);
    }

    /// <summary>
    /// A group needs at least two members to ever fire, so a group with fewer is dead
    /// content that would quietly never run.
    /// </summary>
    [Fact]
    public void Seed_EveryTransitionGroupHasAtLeastTwoMemberRules()
    {
        List<DatingRule> seed = DatingRulesSeed.BuildSeed();

        foreach (DatingTransitionGroup group in DatingRulesSeed.BuildTransitionGroups())
        {
            int members = seed.Count(r =>
                string.Equals(r.TransitionGroupCode, group.Code, StringComparison.OrdinalIgnoreCase));
            Assert.True(members >= 2, $"Transition group {group.Code} has {members} member rule(s); needs 2+.");
        }
    }

    /// <summary>
    /// The rules document's own worked example (§9), run against the SHIPPED seed rather
    /// than test fixtures: a cut-label dress with a dryer symbol, a numbered wash tub and a
    /// bare 01 London number lands on "early-to-mid 1980s".
    ///
    /// Note the numbered tub is a CARE-1986 member but the underline is not present, so
    /// this is a single-member case and bounds normally — the transition path is not
    /// involved. With the default 12-month trailing tolerance the answer is 1980-1987,
    /// which is what the document states ("1980 - 1986/87").
    /// </summary>
    [Fact]
    public void SeedWorkedExample_FromTheRulesDocument_DatesToTheEarlyMid1980s()
    {
        List<GarmentEvidence> evidence =
        [
            Ev(EvidenceType.CareLabel, "tumble_dry_symbol"),
            Ev(EvidenceType.CareLabel, "numbered_wash_tub"),
            Ev(EvidenceType.PhoneNumber, "01-629 1234"),
        ];

        List<DatingRule> rules =
        [
            SeedRule("CARE_TUMBLE_DRY_SYMBOL"),
            SeedRule("CARE_NUMBERED_WASH_TUB"),
            SeedRule("PHONE_UK_BARE_01_LONDON"),
        ];

        DatingAssessment result = Engine().Assess(
            evidence, rules, transitionGroups: DatingRulesSeed.BuildTransitionGroups());

        Assert.Equal(DatingOutcome.Bounded, result.Outcome);
        Assert.Equal(new DateOnly(1980, 1, 1), result.Earliest);
        Assert.Equal(new DateOnly(1987, 12, 31), result.Latest);
        Assert.False(result.HasHardContradiction);
        Assert.Contains("maker unknown", result.Summary);

        // Every firing rule must be able to say why, and on whose authority.
        Assert.All(
            result.Steps.Where(s => s.AppliedToInterval),
            s => Assert.False(string.IsNullOrWhiteSpace(s.SourceCitation)));
    }
}
