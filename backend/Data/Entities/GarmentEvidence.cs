namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// One observation taken from a garment — a care symbol, a zip type, a phone number on a
/// maker's label, a country-of-origin wording.
///
/// This is the unit the rules engine reasons over. The brand label is simply one
/// <see cref="EvidenceType"/> among many, which is what lets a cut-label garment still
/// produce "1980-86, era verified, maker unknown".
/// </summary>
public class GarmentEvidence : BaseEntity
{
    public Guid GarmentId { get; set; }
    public Garment? Garment { get; set; }

    public EvidenceType Type { get; set; }

    /// <summary>
    /// The structured observation, e.g. "tumble_dry_symbol", "numbered_wash_tub",
    /// "01-629-1234", "Made in West Germany". Rules match against this, so it wants to be
    /// a controlled vocabulary wherever possible rather than free prose.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>Optional human note about the observation — where on the garment, condition.</summary>
    public string Notes { get; set; } = "";

    /// <summary>
    /// The archived photograph this observation was read from, when it came from one.
    /// Evidence can also be entered directly (a seller reading a label by eye), so this is
    /// optional — but a capture-backed observation is the one that can be re-checked later.
    /// </summary>
    public Guid? CaptureId { get; set; }
    public GarmentCapture? Capture { get; set; }

    /// <summary>
    /// Convenience pointer to the image this came from. Kept for observations recorded
    /// without a formal capture; prefer <see cref="CaptureId"/> where there is one.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Proposed until a human confirms. A machine-read care symbol is a suggestion; only a
    /// confirmed observation is ground truth, and only confirmed observations are safe to
    /// train anything on later.
    /// </summary>
    public ConfirmationState Confirmation { get; set; } = ConfirmationState.Proposed;

    /// <summary>Who confirmed it, when confirmed.</summary>
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
}
