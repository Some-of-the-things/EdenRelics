using EdenRelics.SellerTool.Dating;

namespace EdenRelics.SellerTool.Dating.Tests;

public class DatingEngineTests
{
    private static DatingRule Rule(string id, string feature, int? notBefore, int? notAfter,
        BoundStrength strength = BoundStrength.Hard, int lagMonths = 0, RuleStatus status = RuleStatus.Verified) =>
        new()
        {
            Id = id, Feature = feature, NotBefore = notBefore, NotAfter = notAfter,
            Strength = strength, TransitionLagMonths = lagMonths, Status = status, SourceCitation = "test",
        };

    private static IDatingEngine Engine(params DatingRule[] rules) =>
        new DatingEngine(new InMemoryRuleStore(rules));

    private static Evidence Feat(string feature) => new(feature, EvidenceType.Other);

    [Fact]
    public void WorkedExample_CutLabelDress_IntersectsTo_1980_1986_AndFlagsA1970sClaim()
    {
        // Brief §3.2: tumble-dry symbol (NOT BEFORE 1980) ∩ numbered wash tub (NOT AFTER 1986)
        // ∩ bare 01 London phone (NOT AFTER 1990) => 1980–1986. No brand knowledge used.
        IDatingEngine engine = Engine(
            Rule("CARE-TD", "care.tumble-dry-symbol", 1980, null),
            Rule("CARE-WT", "care.numbered-wash-tub", null, 1986),
            Rule("PHONE-01", "phone.london-01", null, 1990));

        DatingResult result = engine.Estimate(
            [Feat("care.tumble-dry-symbol"), Feat("care.numbered-wash-tub"), Feat("phone.london-01")],
            claim: new DateInterval(1970, 1979));

        Assert.Equal(new DateInterval(1980, 1986), result.Range);
        Assert.Equal(DatingOutcome.Estimated, result.Outcome);
        Assert.NotNull(result.ClaimFlag);
        Assert.Equal(BoundStrength.Hard, result.ClaimFlag!.Strength);
        Assert.Equal(3, result.Evidence.Count);   // full evidence chain
    }

    [Fact]
    public void EvidenceSet_NotBrandLabel_DatesWithBrandLabelMissing()
    {
        // §3.1: a cut-label garment still dates from care label + zip.
        IDatingEngine engine = Engine(
            Rule("CARE-X", "care.symbol-set-b", 1980, null),
            Rule("ZIP-NYLON", "zip.nylon-coil", null, 1989));

        DatingResult result = engine.Estimate([Feat("care.symbol-set-b"), Feat("zip.nylon-coil")]);

        Assert.Equal(new DateInterval(1980, 1989), result.Range);
        Assert.Equal(DatingOutcome.Estimated, result.Outcome);
    }

    [Fact]
    public void HardEvidence_ThatIntersectsToNothing_IsHardContradiction()
    {
        IDatingEngine engine = Engine(
            Rule("A", "feat.a", 1990, null),
            Rule("B", "feat.b", null, 1985));

        DatingResult result = engine.Estimate([Feat("feat.a"), Feat("feat.b")]);

        Assert.Equal(DatingOutcome.HardContradiction, result.Outcome);
        Assert.True(result.Range.IsEmpty);
    }

    [Fact]
    public void SoftEvidence_ConflictingWithHard_IsSoftContradiction_NotHard()
    {
        IDatingEngine engine = Engine(
            Rule("HARD", "feat.hard", 1980, null),
            Rule("SOFT", "feat.soft", null, 1975, strength: BoundStrength.Soft));

        DatingResult result = engine.Estimate([Feat("feat.hard"), Feat("feat.soft")]);

        Assert.Equal(DatingOutcome.SoftContradiction, result.Outcome);
        // Falls back to the firm (hard) range rather than the impossible intersection.
        Assert.Equal(new DateInterval(1980, null), result.Range);
    }

    [Fact]
    public void UnverifiedRules_NeverAffectOutput()
    {
        IDatingEngine engine = Engine(
            Rule("UNVER", "feat.x", 2000, null, status: RuleStatus.Unverified));

        DatingResult result = engine.Estimate([Feat("feat.x")]);

        Assert.Equal(DateInterval.Unbounded, result.Range);
        Assert.Empty(result.Evidence);
    }

    [Fact]
    public void TransitionLag_ExtendsTrailingEdgeOnly()
    {
        // NOT AFTER 1986 with a 12-month lag => effective NOT AFTER 1987; leading edge is untouched.
        IDatingEngine engine = Engine(Rule("LAG", "feat.x", null, 1986, lagMonths: 12));

        DatingResult result = engine.Estimate([Feat("feat.x")]);

        Assert.Equal(new DateInterval(null, 1987), result.Range);
    }

    [Fact]
    public void RangeRule_FlagsClaimOutsideRange_NotInside()
    {
        // CC41 utility mark: 1941–1952.
        IDatingEngine engine = Engine(Rule("CC41", "mark.cc41", 1941, 1952));

        DatingResult outside = engine.Estimate([Feat("mark.cc41")], claim: new DateInterval(1970, 1979));
        DatingResult inside = engine.Estimate([Feat("mark.cc41")], claim: new DateInterval(1945, 1949));

        Assert.NotNull(outside.ClaimFlag);
        Assert.Equal(BoundStrength.Hard, outside.ClaimFlag!.Strength);
        Assert.Null(inside.ClaimFlag);
    }

    [Fact]
    public void NoMatchingEvidence_YieldsUnboundedEstimate_NoFlag()
    {
        IDatingEngine engine = Engine(Rule("CARE", "care.symbol", 1980, null));

        DatingResult result = engine.Estimate([Feat("something.unrelated")], claim: new DateInterval(1960, 1969));

        Assert.Equal(DateInterval.Unbounded, result.Range);
        Assert.Equal(DatingOutcome.Estimated, result.Outcome);
        Assert.Null(result.ClaimFlag);   // no evidence ⇒ nothing to contradict
    }

    // ── Value matching (rules doc §4-§7: the families that read text off a label) ──

    private static Evidence Val(EvidenceType type, string raw) => new($"{type}.raw", type, raw);

    private static DatingRule ValueRule(
        string id, EvidenceType type, MatchKind match, string pattern,
        int? notBefore, int? notAfter, BoundStrength strength = BoundStrength.Hard) =>
        new()
        {
            Id = id, Feature = id, Match = match, Pattern = pattern, Type = type,
            NotBefore = notBefore, NotAfter = notAfter, Strength = strength,
            Status = RuleStatus.Verified, SourceCitation = "test",
        };

    [Theory]
    [InlineData("01-629 1234", true)]
    [InlineData("01 629 1234", true)]
    [InlineData("071-629 1234", false)]
    [InlineData("0161 236 1234", false)]   // post-1995 provincial 01x1, not a bare London 01
    public void ValueRegex_BareLondon01_MatchesOnlyPre1990Formats(string number, bool shouldBound)
    {
        IDatingEngine engine = new DatingEngine(new InMemoryRuleStore(
            [ValueRule("TEL-01", EvidenceType.PhoneNumber, MatchKind.ValueRegex,
                @"^0\s*1[\s\-]?(?!\d1)\d", null, 1990)]));

        DatingResult r = engine.Estimate([Val(EvidenceType.PhoneNumber, number)]);

        Assert.Equal(shouldBound ? 1990 : null, r.Range.Latest);
    }

    [Fact]
    public void ValueMatching_IgnoresEvidenceOfADifferentType()
    {
        // A fibre rule must not fire on text that happens to sit in a phone-number field.
        IDatingEngine engine = new DatingEngine(new InMemoryRuleStore(
            [ValueRule("FIBRE-02", EvidenceType.Fabric, MatchKind.ValueRegex, @"\blyocell\b", 1998, null)]));

        Assert.Equal(DateInterval.Unbounded, engine.Estimate([Val(EvidenceType.PhoneNumber, "lyocell")]).Range);
        Assert.Equal(1998, engine.Estimate([Val(EvidenceType.Fabric, "55% lyocell")]).Range.Earliest);
    }

    [Fact]
    public void InvalidRegexRule_FailsClosed()
    {
        // Rule content is authored separately from the code that runs it. A broken pattern must
        // never bound a date — it must simply not fire.
        IDatingEngine engine = new DatingEngine(new InMemoryRuleStore(
            [ValueRule("BAD", EvidenceType.Fabric, MatchKind.ValueRegex, "([unclosed", 1990, null)]));

        Assert.Equal(DateInterval.Unbounded, engine.Estimate([Val(EvidenceType.Fabric, "anything")]).Range);
    }

    // ── Transition groups (rules doc §0.4) ────────────────────────────────

    private static TransitionGroup Group(
        string code, int start, int end, RuleStatus status = RuleStatus.Verified, int lagMonths = 0) =>
        new()
        {
            Code = code, Description = code, PeriodStart = start, PeriodEnd = end,
            TransitionLagMonths = lagMonths, Status = status, SourceCitation = "test",
        };

    /// <summary>
    /// The case the group exists for. A numbered wash tub caps at 1986 and an agitation underline
    /// floors at 1986, so intersecting them collapses the answer onto the changeover itself. Both
    /// conventions are documented as coexisting across 1980-1986, so the honest answer is the whole
    /// transition, not the instant the standard changed.
    /// </summary>
    [Fact]
    public void TransitionGroup_CoOccurringRules_WidenInsteadOfCollapsing()
    {
        DatingRule tub = Rule("CARE-05", "care.numbered-wash-tub", null, 1986) with { TransitionGroup = "CARE-1986" };
        DatingRule underline = Rule("CARE-06", "care.wash-tub-underline", 1986, null) with { TransitionGroup = "CARE-1986" };

        IDatingEngine engine = new DatingEngine(new InMemoryRuleStore(
            [tub, underline], [Group("CARE-1986", 1980, 1986)]));

        DatingResult r = engine.Estimate([Feat("care.numbered-wash-tub"), Feat("care.wash-tub-underline")]);

        Assert.Equal(new DateInterval(1980, 1986), r.Range);
        Assert.Equal(DatingOutcome.Estimated, r.Outcome);

        // The member rules stay in the chain, set aside with a reason naming the group.
        RuleContribution member = r.Evidence.Single(e => e.RuleId == "CARE-05");
        Assert.False(member.Applied);
        Assert.Contains("CARE-1986", member.ExclusionReason);
        Assert.Contains(r.Evidence, e => e.RuleId == "CARE-1986" && e.Applied);
    }

    /// <summary>
    /// One convention alone is not a co-occurrence. If a lone member triggered the group, every
    /// numbered tub would widen to the whole transition and the rule would stop being worth anything.
    /// </summary>
    [Fact]
    public void TransitionGroup_SingleMemberRule_BoundsNormally()
    {
        DatingRule tub = Rule("CARE-05", "care.numbered-wash-tub", null, 1986) with { TransitionGroup = "CARE-1986" };

        IDatingEngine engine = new DatingEngine(new InMemoryRuleStore([tub], [Group("CARE-1986", 1980, 1986)]));
        DatingResult r = engine.Estimate([Feat("care.numbered-wash-tub")]);

        Assert.Equal(1986, r.Range.Latest);
        Assert.Null(r.Range.Earliest);   // nothing here says the garment is post-1980
        Assert.True(r.Evidence.Single().Applied);
    }

    [Fact]
    public void TransitionGroup_Unverified_IsInertLikeAnUnverifiedRule()
    {
        DatingRule tub = Rule("CARE-05", "care.numbered-wash-tub", null, 1986) with { TransitionGroup = "CARE-1986" };
        DatingRule underline = Rule("CARE-06", "care.wash-tub-underline", 1986, null) with { TransitionGroup = "CARE-1986" };

        IDatingEngine engine = new DatingEngine(new InMemoryRuleStore(
            [tub, underline], [Group("CARE-1986", 1980, 1986, RuleStatus.Unverified)]));

        // Falls back to plain intersection: the changeover instant, not the transition.
        Assert.Equal(new DateInterval(1986, 1986), engine.Estimate(
            [Feat("care.numbered-wash-tub"), Feat("care.wash-tub-underline")]).Range);
    }

    /// <summary>The widened bound can be no stronger than the evidence that triggered it, or a group
    /// reached through soft rules would launder them into a hard window.</summary>
    [Fact]
    public void TransitionGroup_TriggeredBySoftRules_ProducesASoftWindow()
    {
        DatingRule a = Rule("A", "a", null, 1986, BoundStrength.Soft) with { TransitionGroup = "G" };
        DatingRule b = Rule("B", "b", 1986, null) with { TransitionGroup = "G" };
        DatingRule hard = Rule("H", "h", 1990, null);

        IDatingEngine engine = new DatingEngine(new InMemoryRuleStore([a, b, hard], [Group("G", 1980, 1986)]));
        DatingResult r = engine.Estimate([Feat("a"), Feat("b"), Feat("h")]);

        // The group window is soft, so it cannot contradict the hard 1990 floor into an empty
        // hard range — it degrades to a soft conflict instead.
        Assert.Equal(DatingOutcome.SoftContradiction, r.Outcome);
        Assert.Equal(1990, r.Range.Earliest);
    }

    [Fact]
    public void TransitionGroup_AppliesLagToItsPeriodEndOnly()
    {
        DatingRule a = Rule("A", "a", null, 1986) with { TransitionGroup = "G" };
        DatingRule b = Rule("B", "b", 1986, null) with { TransitionGroup = "G" };

        IDatingEngine engine = new DatingEngine(new InMemoryRuleStore(
            [a, b], [Group("G", 1980, 1986, lagMonths: 12)]));

        Assert.Equal(new DateInterval(1980, 1987), engine.Estimate([Feat("a"), Feat("b")]).Range);
    }

    // ── Provenance (rules doc §0.3) ───────────────────────────────────────

    [Fact]
    public void Provenance_TravelsIntoTheEvidenceChain()
    {
        DatingRule r = Rule("FIBRE-02", "fibre.lyocell", 1998, null) with
        {
            Provenance = ProvenanceClass.PrimaryLegislation,
            SpecId = "FIBRE-02",
        };

        RuleContribution c = new DatingEngine(new InMemoryRuleStore([r]))
            .Estimate([Feat("fibre.lyocell")]).Evidence.Single();

        Assert.Equal(ProvenanceClass.PrimaryLegislation, c.Provenance);
        Assert.Equal("FIBRE-02", c.SpecId);
    }
}
