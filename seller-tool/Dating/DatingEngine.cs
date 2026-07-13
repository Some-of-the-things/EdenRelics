namespace EdenRelics.SellerTool.Dating;

public interface IDatingEngine
{
    /// <summary>Date a garment from its observed evidence. If <paramref name="claim"/> (the seller's
    /// claimed era) is supplied, also report whether it conflicts with the evidence.</summary>
    DatingResult Estimate(IReadOnlyCollection<Evidence> observed, DateInterval? claim = null);
}

/// <summary>
/// Generic interval-intersection dating engine (brief §3.2). Each matching rule contributes a
/// permitted interval; the estimate is what survives their intersection. An empty intersection is a
/// contradiction, surfaced — never silently reconciled. Hard and soft evidence are intersected
/// separately so strength drives behaviour (§3.3). No brand knowledge, no ML — deterministic.
/// </summary>
public sealed class DatingEngine(IRuleStore store) : IDatingEngine
{
    public DatingResult Estimate(IReadOnlyCollection<Evidence> observed, DateInterval? claim = null)
    {
        HashSet<string> features = observed.Select(e => e.Feature).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<DatingRule> matching = store.VerifiedRules().Where(r => features.Contains(r.Feature)).ToList();

        List<RuleContribution> evidence = matching
            .Select(r => new RuleContribution(r.Id, r.Feature, EffectiveInterval(r).ToString(), r.Strength, r.SourceCitation))
            .ToList();

        DateInterval hardRange = matching
            .Where(r => r.Strength == BoundStrength.Hard)
            .Select(EffectiveInterval)
            .Aggregate(DateInterval.Unbounded, (acc, i) => acc.Intersect(i));

        // Hard evidence that intersects to nothing is impossible — misread or fake.
        if (hardRange.IsEmpty)
        {
            return new DatingResult(hardRange, DatingOutcome.HardContradiction, evidence, null);
        }

        DateInterval fullRange = matching
            .Where(r => r.Strength == BoundStrength.Soft)
            .Select(EffectiveInterval)
            .Aggregate(hardRange, (acc, i) => acc.Intersect(i));

        bool softConflict = fullRange.IsEmpty;
        DateInterval range = softConflict ? hardRange : fullRange;
        DatingOutcome outcome = softConflict ? DatingOutcome.SoftContradiction : DatingOutcome.Estimated;

        ClaimFlag? claimFlag = null;
        if (claim is DateInterval claimed)
        {
            if (!claimed.Overlaps(hardRange))
            {
                claimFlag = new ClaimFlag(BoundStrength.Hard,
                    $"Claimed {claimed} contradicts firm evidence dating this to {hardRange}.");
            }
            else if (!claimed.Overlaps(range))
            {
                claimFlag = new ClaimFlag(BoundStrength.Soft,
                    $"Claimed {claimed} sits outside the softer estimate of {range} — worth checking.");
            }
        }

        return new DatingResult(range, outcome, evidence, claimFlag);
    }

    /// <summary>A rule's interval with the trailing-edge transition-lag applied (§3.7): an outdated
    /// feature can linger past a change-over, so NotAfter is extended; NotBefore stays firm.</summary>
    private static DateInterval EffectiveInterval(DatingRule rule)
    {
        int? latest = rule.NotAfter is int notAfter ? notAfter + LagYears(rule) : null;
        return new DateInterval(rule.NotBefore, latest);
    }

    private static int LagYears(DatingRule rule) =>
        rule.TransitionLagMonths <= 0 ? 0 : (int)Math.Ceiling(rule.TransitionLagMonths / 12.0);
}
