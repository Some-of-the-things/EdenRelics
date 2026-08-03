using System.Text.RegularExpressions;
using EdenRelics.SellerTool.Data;
using EdenRelics.SellerTool.Dating;

namespace EdenRelics.SellerTool.Data.Tests;

/// <summary>
/// Assertions on the SHIPPED rule content. A mis-scoped regex, or a rule shipped Verified without a
/// source, is a content bug — and content bugs in here flag correct listings as wrong, which is the
/// one failure the whole product is built to avoid.
/// </summary>
public class DatingRulesSeedTests
{
    [Fact]
    public void EveryRuleCarriesItsSpecIdAndAUniqueId()
    {
        List<StoredRule> seed = DatingRulesSeed.BuildRules();

        Assert.NotEmpty(seed);
        Assert.All(seed, r => Assert.False(string.IsNullOrWhiteSpace(r.SpecId), $"{r.Id} has no SpecId"));
        // Ids must stay unique — the seeder reconciles on Id, so a duplicate would make one of the
        // pair unreachable and silently un-updatable.
        Assert.Equal(seed.Count, seed.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The standing warning from §12 of the rules document, enforced. Anything still awaiting a
    /// source must be inert, and "PENDING" in a citation is how the seed marks that.
    /// </summary>
    [Fact]
    public void NoVerifiedRuleHasAPendingCitation()
    {
        List<string> offenders = DatingRulesSeed.BuildRules()
            .Where(r => r.Status == RuleStatus.Verified
                && r.SourceCitation?.Contains("PENDING", StringComparison.OrdinalIgnoreCase) == true)
            .Select(r => r.Id)
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The engine deliberately fails a bad regex CLOSED — it just does not fire. That is right at
    /// runtime and useless as feedback, so an unparseable pattern would ship as a rule that silently
    /// never matches. Catch it here instead.
    /// </summary>
    [Fact]
    public void EveryValueRuleHasAUsablePattern()
    {
        foreach (StoredRule rule in DatingRulesSeed.BuildRules()
            .Where(r => r.Match is MatchKind.ValueRegex or MatchKind.ValueContains))
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Pattern), $"{rule.Id} matches on value but has no pattern");
            if (rule.Match != MatchKind.ValueRegex)
            {
                continue;
            }
            Exception? failure = Record.Exception(() => Regex.IsMatch("probe", rule.Pattern!));
            Assert.True(failure is null, $"{rule.Id} has an invalid regex: {failure?.Message}");
        }
    }

    [Fact]
    public void TransitionGroupMembersAllReferToAGroupThatExists()
    {
        HashSet<string> groups = DatingRulesSeed.BuildTransitionGroups()
            .Select(g => g.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<string> dangling = DatingRulesSeed.BuildRules()
            .Where(r => r.TransitionGroup is not null && !groups.Contains(r.TransitionGroup))
            .Select(r => r.Id)
            .ToList();

        Assert.Empty(dangling);
    }

    /// <summary>A group needs two members to ever fire, so one with fewer is dead content.</summary>
    [Fact]
    public void EveryTransitionGroupHasAtLeastTwoMemberRules()
    {
        List<StoredRule> seed = DatingRulesSeed.BuildRules();

        foreach (StoredTransitionGroup group in DatingRulesSeed.BuildTransitionGroups())
        {
            int members = seed.Count(r =>
                string.Equals(r.TransitionGroup, group.Code, StringComparison.OrdinalIgnoreCase));
            Assert.True(members >= 2, $"Transition group {group.Code} has {members} member rule(s); needs 2+.");
        }
    }

    /// <summary>
    /// The rules document's own worked example (§9), run against the SHIPPED rules rather than test
    /// fixtures: a cut-label dress with a dryer symbol, a numbered wash tub and a bare 01 London
    /// number lands on "early-to-mid 1980s".
    ///
    /// The numbered tub is a CARE-1986 member but the underline is absent, so this is a
    /// single-member case and bounds normally — the transition path is not involved. With the
    /// 12-month trailing lag the answer is 1980-1987, which is what the document states
    /// ("1980 - 1986/87").
    /// </summary>
    [Fact]
    public void WorkedExampleFromTheRulesDocument_DatesToTheEarlyMid1980s()
    {
        IRuleStore store = new InMemoryRuleStore(
            DatingRulesSeed.BuildRules().Select(r => r.ToDomain()),
            DatingRulesSeed.BuildTransitionGroups().Select(g => g.ToDomain()));

        DatingResult result = new DatingEngine(store).Estimate(
        [
            new Evidence("care.tumble-dry-symbol", EvidenceType.CareLabel),
            new Evidence("care.numbered-wash-tub", EvidenceType.CareLabel),
            new Evidence("phone.raw", EvidenceType.PhoneNumber, "01-629 1234"),
        ]);

        Assert.Equal(DatingOutcome.Estimated, result.Outcome);
        Assert.Equal(new DateInterval(1980, 1987), result.Range);

        // Every firing rule must be able to say why, and on whose authority.
        Assert.All(result.Evidence.Where(e => e.Applied),
            e => Assert.False(string.IsNullOrWhiteSpace(e.Source)));
    }

    /// <summary>
    /// The co-occurrence case, against shipped content: a numbered tub AND an agitation underline
    /// widen to the documented 1980-1986 transition rather than collapsing onto 1986.
    /// </summary>
    [Fact]
    public void ShippedCare1986Group_WidensOnCoOccurrence()
    {
        IRuleStore store = new InMemoryRuleStore(
            DatingRulesSeed.BuildRules().Select(r => r.ToDomain()),
            DatingRulesSeed.BuildTransitionGroups().Select(g => g.ToDomain()));

        DatingResult result = new DatingEngine(store).Estimate(
        [
            new Evidence("care.numbered-wash-tub", EvidenceType.CareLabel),
            new Evidence("care.wash-tub-underline", EvidenceType.CareLabel),
        ]);

        Assert.Equal(new DateInterval(1980, 1987), result.Range);
        Assert.Contains(result.Evidence, e => e.RuleId == "CARE-1986" && e.Applied);
        Assert.Contains(result.Evidence, e => e.RuleId == "CARE_NUMBERED_WASH_TUB" && !e.Applied);
    }
}
