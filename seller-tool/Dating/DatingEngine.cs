using System.Text.RegularExpressions;

namespace EdenRelics.SellerTool.Dating;

public interface IDatingEngine
{
    /// <summary>Date a garment from its observed evidence. If <paramref name="claim"/> (the seller's
    /// claimed era) is supplied, also report whether it conflicts with the evidence.</summary>
    DatingResult Estimate(IReadOnlyCollection<Evidence> observed, DateInterval? claim = null);
}

/// <summary>Tunables for the engine.</summary>
public sealed class DatingOptions
{
    /// <summary>Ceiling on evaluating one regex rule. Rule content is authored separately from the
    /// code that runs it, so a pathological pattern must not be able to hang a request.</summary>
    public int RegexTimeoutMs { get; set; } = 100;
}

/// <summary>
/// Generic interval-intersection dating engine (brief §3.2). Each matching rule contributes a
/// permitted interval; the estimate is what survives their intersection. An empty intersection is a
/// contradiction, surfaced — never silently reconciled. Hard and soft evidence are intersected
/// separately so strength drives behaviour (§3.3). No brand knowledge, no ML — deterministic.
///
/// One exception to "intersect": a transition group WIDENS on co-occurrence. See
/// <see cref="TransitionGroup"/>.
/// </summary>
public sealed class DatingEngine(IRuleStore store, DatingOptions? options = null) : IDatingEngine
{
    private readonly DatingOptions _options = options ?? new DatingOptions();

    public DatingResult Estimate(IReadOnlyCollection<Evidence> observed, DateInterval? claim = null)
    {
        List<DatingRule> matching = store.VerifiedRules().Where(r => Matches(r, observed)).ToList();

        // Widen before intersecting: a co-occurrence a transition group explains is not evidence of
        // a precise date, so its members must be out of the way before the intersection runs.
        HashSet<string> superseded = new(StringComparer.OrdinalIgnoreCase);
        List<(TransitionGroup Group, BoundStrength Strength, string Members)> widened = [];

        foreach (TransitionGroup group in store.TransitionGroups()
            .Where(g => g.Status == RuleStatus.Verified)
            .OrderBy(g => g.Code, StringComparer.Ordinal))
        {
            List<DatingRule> members = matching
                .Where(r => string.Equals(r.TransitionGroup, group.Code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (members.Select(m => m.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2)
            {
                continue;
            }

            foreach (DatingRule m in members)
            {
                superseded.Add(m.Id);
            }
            // The widened bound can be no stronger than the weakest evidence that triggered it, or a
            // group reached through soft rules would launder them into a hard window.
            BoundStrength strength = members.Any(m => m.Strength == BoundStrength.Soft)
                ? BoundStrength.Soft
                : BoundStrength.Hard;
            widened.Add((group, strength, string.Join(", ", members.Select(m => m.Id).Distinct().OrderBy(x => x, StringComparer.Ordinal))));
        }

        List<RuleContribution> evidence = matching
            .Select(r => new RuleContribution(
                r.Id, r.Feature, EffectiveInterval(r).ToString(), r.Strength, r.SourceCitation,
                r.Provenance, r.SpecId,
                Applied: !superseded.Contains(r.Id),
                ExclusionReason: superseded.Contains(r.Id)
                    ? $"Superseded by transition group {r.TransitionGroup}: this convention is documented as "
                      + "coexisting with the others found here, so the co-occurrence widens the window rather than narrowing it."
                    : null))
            .ToList();

        evidence.AddRange(widened.Select(w => new RuleContribution(
            w.Group.Code, w.Group.Code, GroupInterval(w.Group).ToString(), w.Strength,
            w.Group.SourceCitation, w.Group.Provenance, w.Group.Code,
            Applied: true,
            ExclusionReason: null)));

        List<(DateInterval Interval, BoundStrength Strength)> bounds =
        [
            .. matching.Where(r => !superseded.Contains(r.Id)).Select(r => (EffectiveInterval(r), r.Strength)),
            .. widened.Select(w => (GroupInterval(w.Group), w.Strength)),
        ];

        DateInterval hardRange = bounds
            .Where(b => b.Strength == BoundStrength.Hard)
            .Select(b => b.Interval)
            .Aggregate(DateInterval.Unbounded, (acc, i) => acc.Intersect(i));

        // Hard evidence that intersects to nothing is impossible — misread or fake.
        if (hardRange.IsEmpty)
        {
            return new DatingResult(hardRange, DatingOutcome.HardContradiction, evidence, null);
        }

        DateInterval fullRange = bounds
            .Where(b => b.Strength == BoundStrength.Soft)
            .Select(b => b.Interval)
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

    /// <summary>Whether any observation satisfies this rule's test.</summary>
    private bool Matches(DatingRule rule, IReadOnlyCollection<Evidence> observed) => rule.Match switch
    {
        MatchKind.Feature => observed.Any(e => string.Equals(e.Feature, rule.Feature, StringComparison.OrdinalIgnoreCase)),
        MatchKind.ValueContains => !string.IsNullOrEmpty(rule.Pattern)
            && observed.Any(e => e.Type == rule.Type
                && e.RawValue?.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase) == true),
        MatchKind.ValueRegex => !string.IsNullOrEmpty(rule.Pattern)
            && observed.Any(e => e.Type == rule.Type && RegexMatches(rule, e.RawValue)),
        _ => false,
    };

    private bool RegexMatches(DatingRule rule, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        try
        {
            return Regex.IsMatch(value, rule.Pattern!,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(_options.RegexTimeoutMs));
        }
        catch (RegexMatchTimeoutException)
        {
            // A rule that cannot be evaluated must never silently bound a date.
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>A rule's interval with the trailing-edge transition-lag applied (§3.7): an outdated
    /// feature can linger past a change-over, so NotAfter is extended; NotBefore stays firm.</summary>
    private static DateInterval EffectiveInterval(DatingRule rule)
    {
        int? latest = rule.NotAfter is int notAfter ? notAfter + LagYears(rule.TransitionLagMonths) : null;
        return new DateInterval(rule.NotBefore, latest);
    }

    private static DateInterval GroupInterval(TransitionGroup group) =>
        new(group.PeriodStart, group.PeriodEnd + LagYears(group.TransitionLagMonths));

    private static int LagYears(int lagMonths) =>
        lagMonths <= 0 ? 0 : (int)Math.Ceiling(lagMonths / 12.0);
}
