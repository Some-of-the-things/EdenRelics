using System.Text.RegularExpressions;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Services;

/// <summary>Tunables for the dating engine.</summary>
public class DatingOptions
{
    /// <summary>
    /// Trailing-edge tolerance applied when a rule does not specify its own, in months.
    /// Roughly a year: printed label stock got used up and finished garments sat in
    /// warehouses, so a change's effective date and its appearance on garments differ.
    /// Configurable so it can be tuned against real data once we have some.
    /// </summary>
    public int DefaultTrailingToleranceMonths { get; set; } = 12;

    /// <summary>
    /// Ceiling on evaluating a single regex test. Rule content is authored separately from
    /// the code that runs it, so a pathological pattern must not be able to hang ingest.
    /// </summary>
    public int RegexTimeoutMs { get; set; } = 100;
}

/// <inheritdoc />
public class DatingRulesEngine(
    IRepository<Garment> garments,
    IRepository<GarmentEvidence> evidenceRepo,
    IRepository<DatingRule> rules,
    IRepository<DatingAssessment> assessments,
    IOptions<DatingOptions> options,
    ILogger<DatingRulesEngine> logger) : IDatingRulesEngine
{
    private readonly DatingOptions _options = options.Value;

    public DatingAssessment Assess(
        IReadOnlyCollection<GarmentEvidence> evidence,
        IReadOnlyCollection<DatingRule> ruleSet,
        DateOnly? claimedEraStart = null,
        DateOnly? claimedEraEnd = null)
    {
        // Rejected observations are struck from the record entirely; unverified rules exist
        // only so the research can be staged and must never influence an answer.
        List<GarmentEvidence> usable = evidence
            .Where(e => e.Confirmation != ConfirmationState.Rejected)
            .ToList();
        List<DatingRule> active = ruleSet
            .Where(r => r.Status == RuleStatus.Active)
            .ToList();

        List<DatingAssessmentStep> steps = [];
        foreach (DatingRule rule in active)
        {
            foreach (GarmentEvidence observation in usable.Where(e => e.Type == rule.EvidenceType))
            {
                if (!Matches(rule, observation))
                {
                    continue;
                }
                steps.Add(BuildStep(rule, observation));
            }
        }

        DatingAssessment assessment = new()
        {
            Steps = steps,
            ContradictsClaimedEra = false,
        };

        if (steps.Count == 0)
        {
            assessment.Outcome = DatingOutcome.Unbounded;
            assessment.Summary = "No dating evidence — nothing bounds this garment yet.";
            assessment.Confirmation = ConfirmationState.Proposed;
            return assessment;
        }

        // Presence is hard, absence is soft, so the hard bounds establish the window and the
        // soft ones may only narrow it further. Applying them separately is what lets a soft
        // contradiction lower confidence instead of blocking a listing.
        (DateOnly? hardStart, DateOnly? hardEnd) = Intersect(steps.Where(s => s.Strength == RuleStrength.Hard));

        if (IsEmpty(hardStart, hardEnd))
        {
            assessment.Outcome = DatingOutcome.Contradiction;
            assessment.HasHardContradiction = true;
            assessment.Earliest = hardStart;
            assessment.Latest = hardEnd;
            assessment.Summary =
                $"Contradiction: the evidence requires both not-before {hardStart:yyyy-MM-dd} and not-after {hardEnd:yyyy-MM-dd}. "
                + "Either an observation is misread or the garment is not what it appears to be.";
            assessment.Confirmation = ConfirmationState.Proposed;
            return assessment;
        }

        DateOnly? start = hardStart;
        DateOnly? end = hardEnd;

        // Fold soft bounds in one at a time, in a stable order, so the outcome does not
        // depend on the order rules came back from the database. Any soft bound that would
        // empty the window is set aside rather than allowed to force a contradiction.
        foreach (DatingAssessmentStep step in steps
            .Where(s => s.Strength == RuleStrength.Soft)
            .OrderBy(s => s.EffectiveBoundDate)
            .ThenBy(s => s.RuleCode, StringComparer.Ordinal))
        {
            (DateOnly? candidateStart, DateOnly? candidateEnd) = ApplyBound(start, end, step);
            if (IsEmpty(candidateStart, candidateEnd))
            {
                step.AppliedToInterval = false;
                step.ExclusionReason =
                    "Soft bound conflicts with the window established by hard evidence; recorded but not applied.";
                assessment.HasSoftContradiction = true;
                continue;
            }
            start = candidateStart;
            end = candidateEnd;
        }

        assessment.Outcome = DatingOutcome.Bounded;
        assessment.Earliest = start;
        assessment.Latest = end;
        assessment.ContradictsClaimedEra = ClaimConflicts(start, end, claimedEraStart, claimedEraEnd);
        assessment.Summary = Describe(start, end, usable, assessment.HasSoftContradiction);

        // An assessment is only as confirmed as the observations underneath it. One
        // machine-proposed reading anywhere in the chain keeps the whole thing a proposal.
        HashSet<Guid> contributing = steps.Where(s => s.AppliedToInterval).Select(s => s.EvidenceId).ToHashSet();
        bool allConfirmed = usable
            .Where(e => contributing.Contains(e.Id))
            .All(e => e.Confirmation == ConfirmationState.HumanConfirmed);
        assessment.Confirmation = allConfirmed ? ConfirmationState.HumanConfirmed : ConfirmationState.Proposed;

        return assessment;
    }

    public async Task<DatingAssessment?> AssessGarmentAsync(Guid garmentId, CancellationToken ct = default)
    {
        Garment? garment = await garments.Query().FirstOrDefaultAsync(g => g.Id == garmentId, ct);
        if (garment is null)
        {
            return null;
        }

        List<GarmentEvidence> evidence = await evidenceRepo.Query()
            .Where(e => e.GarmentId == garmentId)
            .ToListAsync(ct);

        // Only active rules are fetched, but Assess re-filters: it is also called directly
        // in tests and from draft flows, and the guarantee belongs with the engine.
        List<DatingRule> active = await rules.Query()
            .Where(r => r.Status == RuleStatus.Active)
            .ToListAsync(ct);

        DatingAssessment assessment = Assess(evidence, active, garment.ClaimedEraStart, garment.ClaimedEraEnd);
        assessment.GarmentId = garmentId;

        await assessments.AddAsync(assessment);

        logger.LogInformation(
            "Dated garment {GarmentId}: {Outcome} {Earliest}..{Latest} from {StepCount} rule hits (hard contradiction: {Hard})",
            garmentId, assessment.Outcome, assessment.Earliest, assessment.Latest,
            assessment.Steps.Count, assessment.HasHardContradiction);

        return assessment;
    }

    /// <summary>Whether a rule's test is satisfied by an observation.</summary>
    private bool Matches(DatingRule rule, GarmentEvidence observation)
    {
        string value = observation.Value ?? "";
        switch (rule.TestKind)
        {
            case EvidenceTestKind.Present:
                return true;
            case EvidenceTestKind.ValueEquals:
                return string.Equals(value, rule.TestValue, StringComparison.OrdinalIgnoreCase);
            case EvidenceTestKind.ValueContains:
                return !string.IsNullOrEmpty(rule.TestValue)
                    && value.Contains(rule.TestValue, StringComparison.OrdinalIgnoreCase);
            case EvidenceTestKind.ValueMatchesRegex:
                return MatchesRegex(rule, value);
            default:
                return false;
        }
    }

    private bool MatchesRegex(DatingRule rule, string value)
    {
        if (string.IsNullOrEmpty(rule.TestValue))
        {
            return false;
        }
        try
        {
            return Regex.IsMatch(
                value,
                rule.TestValue,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(_options.RegexTimeoutMs));
        }
        catch (RegexMatchTimeoutException)
        {
            // A rule that cannot be evaluated must not silently bound a date.
            logger.LogWarning("Dating rule {Code} regex timed out; treated as no match", rule.Code);
            return false;
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Dating rule {Code} has an invalid regex; treated as no match", rule.Code);
            return false;
        }
    }

    private DatingAssessmentStep BuildStep(DatingRule rule, GarmentEvidence observation)
    {
        // Leading edges get no tolerance: a garment cannot carry a format that did not yet
        // exist. Trailing edges do, because old label stock outlives the change it records.
        int tolerance = rule.BoundType == DateBoundType.NotAfter
            ? rule.TrailingToleranceMonths ?? _options.DefaultTrailingToleranceMonths
            : 0;

        return new DatingAssessmentStep
        {
            RuleId = rule.Id,
            RuleCode = rule.Code,
            RuleDescription = rule.Description,
            SourceCitation = rule.SourceCitation,
            EvidenceId = observation.Id,
            EvidenceType = observation.Type,
            EvidenceValue = observation.Value,
            BoundType = rule.BoundType,
            Strength = rule.Strength,
            BoundDate = rule.BoundDate,
            EffectiveBoundDate = rule.BoundDate.AddMonths(tolerance),
            ToleranceMonthsApplied = tolerance,
            AppliedToInterval = true,
        };
    }

    /// <summary>Intersects a set of bounds: the latest not-before against the earliest not-after.</summary>
    private static (DateOnly? Start, DateOnly? End) Intersect(IEnumerable<DatingAssessmentStep> steps)
    {
        DateOnly? start = null;
        DateOnly? end = null;
        foreach (DatingAssessmentStep step in steps)
        {
            (start, end) = ApplyBound(start, end, step);
        }
        return (start, end);
    }

    private static (DateOnly? Start, DateOnly? End) ApplyBound(
        DateOnly? start, DateOnly? end, DatingAssessmentStep step)
    {
        if (step.BoundType == DateBoundType.NotBefore)
        {
            return (start is null || step.EffectiveBoundDate > start ? step.EffectiveBoundDate : start, end);
        }
        return (start, end is null || step.EffectiveBoundDate < end ? step.EffectiveBoundDate : end);
    }

    private static bool IsEmpty(DateOnly? start, DateOnly? end) => start is not null && end is not null && start > end;

    /// <summary>True when the seller's claim lies wholly outside the surviving window.</summary>
    private static bool ClaimConflicts(DateOnly? start, DateOnly? end, DateOnly? claimStart, DateOnly? claimEnd)
    {
        if (claimStart is null && claimEnd is null)
        {
            return false;
        }
        // Treat a one-sided claim as a point: "they said 1970s" with no end is still checkable
        // against a not-before bound.
        DateOnly effectiveClaimStart = claimStart ?? claimEnd!.Value;
        DateOnly effectiveClaimEnd = claimEnd ?? claimStart!.Value;

        if (end is not null && effectiveClaimStart > end)
        {
            return true;
        }
        return start is not null && effectiveClaimEnd < start;
    }

    private static string Describe(
        DateOnly? start, DateOnly? end, IReadOnlyCollection<GarmentEvidence> evidence, bool softConflict)
    {
        string window = (start, end) switch
        {
            (not null, not null) when start.Value.Year == end.Value.Year => $"{start.Value.Year}",
            (not null, not null) => $"{start.Value.Year}-{end.Value.Year}",
            (not null, null) => $"{start.Value.Year} or later",
            (null, not null) => $"{end.Value.Year} or earlier",
            _ => "undetermined",
        };

        // Naming the missing maker explicitly is the point of the whole evidence-set model:
        // a cut-label garment with a verified era is a sellable product, not a gap.
        bool hasMaker = evidence.Any(e => e.Type == EvidenceType.BrandLabel);
        string maker = hasMaker ? "" : ", maker unknown";
        string caveat = softConflict ? " (some supporting evidence disagrees — confidence reduced)" : "";
        return $"{window}, era verified{maker}{caveat}";
    }
}
