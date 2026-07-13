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
}
