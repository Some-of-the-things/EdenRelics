namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// The result of running the rules engine over a garment's evidence set: a bounded era,
/// or a contradiction, together with the full chain of reasoning that produced it.
///
/// The chain is the point. When the system says "post-1980" it must be able to say which
/// rule and which source — that is the whole difference between this and an app that
/// guesses, and it is what makes an insurable guarantee possible later.
/// </summary>
public class DatingAssessment : BaseEntity
{
    public Guid GarmentId { get; set; }
    public Garment? Garment { get; set; }

    public DatingOutcome Outcome { get; set; }

    /// <summary>
    /// The surviving window. Null on either side means unbounded in that direction — a
    /// garment can be known to be post-1980 with no upper bound at all, and that is a
    /// legitimate, useful answer.
    /// </summary>
    public DateOnly? Earliest { get; set; }
    public DateOnly? Latest { get; set; }

    /// <summary>
    /// True when a HARD bound is irreconcilable. Safe to hold a listing for review: a
    /// symbol that did not exist before 1980 cannot appear on a 1975 garment.
    /// </summary>
    public bool HasHardContradiction { get; set; }

    /// <summary>
    /// True when the contradiction only appears once SOFT bounds are included. Lowers
    /// confidence; must not block a listing, because absence evidence proves much less
    /// than presence evidence.
    /// </summary>
    public bool HasSoftContradiction { get; set; }

    /// <summary>
    /// True when the seller's claimed era falls outside the surviving window. The headline
    /// output of the whole system, and the metric that tells us whether the verification
    /// thesis holds: flags raised versus how often the seller was actually wrong.
    /// </summary>
    public bool ContradictsClaimedEra { get; set; }

    /// <summary>
    /// Proposed until a human confirms. An assessment built on unconfirmed observations is
    /// itself only a proposal, however sound the rules are.
    /// </summary>
    public ConfirmationState Confirmation { get; set; } = ConfirmationState.Proposed;

    /// <summary>Human-readable summary, e.g. "1980-1986, era verified, maker unknown".</summary>
    public string Summary { get; set; } = "";

    /// <summary>Every rule that fired, in the order applied.</summary>
    public List<DatingAssessmentStep> Steps { get; set; } = [];
}
