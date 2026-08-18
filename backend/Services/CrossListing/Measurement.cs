namespace Eden_Relics_BE.Services.CrossListing;

/// <summary>
/// What established the scale in the photograph. Per the measurement addendum (which supersedes the
/// crosslisting brief §5), cards and printed markers are the same technique with a different
/// reference object, so this is one pipeline that accepts corner references of any known size —
/// adding another reference type later costs nothing.
/// </summary>
public enum MeasurementReferenceKind
{
    /// <summary>No reference object. Nothing can be scaled from the photo; hand-measured only.</summary>
    None,

    /// <summary>One ID-1 card. Scale at one end only — no perspective correction.</summary>
    OneCard,

    /// <summary>
    /// Two ID-1 cards at opposite corners. The default: zero setup, works on the first garment in
    /// the first five minutes. A tool that needs a printer before it does anything gets abandoned
    /// during onboarding.
    /// </summary>
    TwoCards,

    /// <summary>
    /// Four separate single-marker A4 sheets, one per corner — deliberately not one large mat, which
    /// would need A1/A0 and so a printer nobody has at home. Spans the full measurement area.
    /// Offered as an upgrade, never required.
    /// </summary>
    FourPrintedMarkers,
}

/// <summary>
/// How much weight a measurement carries, which the addendum ties to the reference type rather than
/// to nagging people into printing things.
/// </summary>
public enum MeasurementConfidence
{
    /// <summary>
    /// We do not yet know how accurate this reference type is. The addendum requires a spike —
    /// two cards, one card and four markers measured against hand measurements on ~10 real garments —
    /// before any reference type is trusted to flow through on a glance. Until that has happened,
    /// every scaled measurement lands here.
    /// </summary>
    Unvalidated,

    /// <summary>Card-based once validated: usable, but more measurements get flagged for a closer look.</summary>
    Lower,

    /// <summary>Marker-based once validated: spans the garment, so more flows through on a glance.</summary>
    Higher,
}

/// <summary>Physical facts and behaviour of each reference type.</summary>
/// <param name="Corners">How many corners of the garment area carry a reference.</param>
/// <param name="CorrectsPerspective">Whether skew can be corrected — needs references at both ends.</param>
/// <param name="RequiresPrinter">Whether the seller has to print anything before they can start.</param>
/// <param name="Guidance">What to tell the seller, in the words they need at the point of use.</param>
public sealed record ReferenceProfile(
    MeasurementReferenceKind Kind,
    int Corners,
    bool CorrectsPerspective,
    bool RequiresPrinter,
    string Guidance);

public static class MeasurementReferences
{
    /// <summary>
    /// ID-1, per ISO/IEC 7810 — 85.60 × 53.98mm. Every debit, loyalty, library and gym card is this
    /// size, which is what makes a card a reliable reference the seller already owns.
    /// </summary>
    public const decimal Id1CardWidthMm = 85.60m;
    public const decimal Id1CardHeightMm = 53.98m;

    private static readonly Dictionary<MeasurementReferenceKind, ReferenceProfile> Profiles = new()
    {
        [MeasurementReferenceKind.None] = new(
            MeasurementReferenceKind.None, 0, false, false,
            "No reference object in the photo, so nothing can be measured from it."),

        [MeasurementReferenceKind.OneCard] = new(
            MeasurementReferenceKind.OneCard, 1, false, false,
            "Place any bank or loyalty card face down at one corner of the garment."),

        [MeasurementReferenceKind.TwoCards] = new(
            MeasurementReferenceKind.TwoCards, 2, true, false,
            "Place two cards face down at opposite corners of the garment. Face down so nothing on "
            + "them is readable. Any two cards will do — they are all the same size."),

        [MeasurementReferenceKind.FourPrintedMarkers] = new(
            MeasurementReferenceKind.FourPrintedMarkers, 4, true, true,
            "Print the four marker sheets and place one at each corner of the garment. Ordinary A4 — "
            + "one marker per sheet, no large mat needed."),
    };

    public static ReferenceProfile Profile(MeasurementReferenceKind kind) => Profiles[kind];

    /// <summary>The order to present options in: no setup first, upgrade second.</summary>
    public static IReadOnlyList<ReferenceProfile> Offered =>
    [
        Profiles[MeasurementReferenceKind.TwoCards],
        Profiles[MeasurementReferenceKind.FourPrintedMarkers],
    ];

    /// <summary>
    /// The confidence a reference type earns.
    ///
    /// Everything scaled is <see cref="MeasurementConfidence.Unvalidated"/> until the addendum's spike
    /// has been run and its thresholds recorded here. Publishing a number we have never checked
    /// against a tape measure is exactly the failure the addendum calls the worst available one: a
    /// wrong measurement causes a return, and returns destroy trust in a tool whose pitch is that you
    /// can believe what it says.
    /// </summary>
    public static MeasurementConfidence ConfidenceFor(MeasurementReferenceKind kind, bool spikeValidated = false)
    {
        if (kind == MeasurementReferenceKind.None)
        {
            return MeasurementConfidence.Unvalidated;
        }
        if (!spikeValidated)
        {
            return MeasurementConfidence.Unvalidated;
        }
        return kind == MeasurementReferenceKind.FourPrintedMarkers
            ? MeasurementConfidence.Higher
            : MeasurementConfidence.Lower;
    }
}

/// <summary>One measurement of a garment, with where its number came from and who has vouched for it.</summary>
/// <param name="Name">e.g. "Pit to pit", "Length", "Waist".</param>
/// <param name="ValueCm">The measurement itself.</param>
/// <param name="Reference">What established the scale.</param>
/// <param name="Confidence">How much weight it carries, from the reference type.</param>
/// <param name="SellerConfirmed">
/// Whether a human has accepted or corrected it. A machine proposal must never reach a live listing,
/// and must never enter the archive as fact.
/// </param>
public sealed record GarmentMeasurement(
    string Name,
    decimal ValueCm,
    MeasurementReferenceKind Reference,
    MeasurementConfidence Confidence,
    bool SellerConfirmed)
{
    /// <summary>A hand measurement: no reference object, and a person is the source.</summary>
    public static GarmentMeasurement HandMeasured(string name, decimal valueCm) =>
        new(name, valueCm, MeasurementReferenceKind.None, MeasurementConfidence.Higher, SellerConfirmed: true);

    /// <summary>A machine proposal from a photograph. Unconfirmed by construction.</summary>
    public static GarmentMeasurement Proposed(
        string name, decimal valueCm, MeasurementReferenceKind reference, bool spikeValidated = false) =>
        new(name, valueCm, reference,
            MeasurementReferences.ConfidenceFor(reference, spikeValidated), SellerConfirmed: false);

    /// <summary>May this number go on a live listing?</summary>
    public bool IsPublishable => SellerConfirmed;

    /// <summary>
    /// Should the seller be asked to look at this one specifically? Confident detections flow through;
    /// low-confidence ones are flagged, so the glance is only required on the hard cases.
    /// </summary>
    public bool NeedsSellerReview =>
        !SellerConfirmed && Confidence != MeasurementConfidence.Higher;
}
