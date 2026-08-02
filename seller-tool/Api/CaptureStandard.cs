using EdenRelics.SellerTool.Data;

namespace EdenRelics.SellerTool.Api;

/// <summary>
/// The fixed capture standard. "To a fixed standard" is the whole value of the archive: a pile of
/// inconsistently-shot photos is not a moat, it is a folder.
///
/// The resolution floors exist because these images have a second job. A listing photo only has to
/// look right; an archive photo has to still be readable when something tries to recognise a care
/// symbol or a stamped zip puller from it years later. Accepting a 400px snap of a care label costs
/// nothing today and silently poisons the archive it is meant to build.
/// </summary>
public static class CaptureStandard
{
    /// <summary>Stamped onto each capture, so a later standard change stays legible in the data.</summary>
    public const string Version = "capture-standard-v1";

    /// <summary>
    /// Slots a listing must have before it counts as fully captured. The brand label is deliberately
    /// NOT among them — a large share of real vintage stock has it cut out, and requiring it would
    /// either block honest listings or teach sellers to photograph the wrong thing.
    /// </summary>
    public static readonly CaptureSlot[] RequiredSlots =
    [
        CaptureSlot.CareLabel,
        CaptureSlot.FlatLayFront,
    ];

    /// <summary>Slots we always want if they exist. Missing ones are prompted for, never enforced.</summary>
    public static readonly CaptureSlot[] RequestedSlots =
    [
        CaptureSlot.BrandLabel,
        CaptureSlot.FlatLayBack,
    ];

    public const long MaxBytes = 25L * 1024 * 1024;

    /// <summary>
    /// Formats a phone camera actually produces. HEIC is deliberately excluded: it needs a decoder
    /// we do not ship, and silently accepting bytes we cannot read would put unusable files in the
    /// archive — the one place that must not happen.
    /// </summary>
    public static readonly string[] AcceptedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
    ];

    /// <summary>
    /// Minimum long-edge pixels per slot. Labels need more than the garment does — the text and
    /// symbols on them are small, and they are the part with the long-term value.
    /// </summary>
    public static int MinimumLongEdge(CaptureSlot slot) => slot switch
    {
        CaptureSlot.BrandLabel or CaptureSlot.CareLabel => 1200,
        CaptureSlot.Zip or CaptureSlot.ConstructionDetail => 1000,
        CaptureSlot.FlatLayFront or CaptureSlot.FlatLayBack => 1200,
        _ => 800,
    };

    /// <summary>Long edge of the generated web derivative.</summary>
    public const int DisplayLongEdge = 1600;
    public const int DisplayQuality = 78;

    /// <summary>Guidance shown next to each slot in the capture UI.</summary>
    public static string Guidance(CaptureSlot slot) => slot switch
    {
        CaptureSlot.BrandLabel =>
            "Fill the frame with the maker's label. If the label has been cut out, skip this — it is expected.",
        CaptureSlot.CareLabel =>
            "Fill the frame with the care label. Include every symbol, and any printed address or phone number "
            + "— those often date the garment more precisely than the brand does.",
        CaptureSlot.FlatLayFront =>
            "Whole garment, flat and square to the camera, on a plain background. This is what measurements come from.",
        CaptureSlot.FlatLayBack => "Whole garment, flat, reverse side.",
        CaptureSlot.Zip =>
            "Close on the zip pull and tape, including any maker's stamp. Record whether it is original or replaced "
            + "— a replaced zip mis-teaches the archive if it is logged unmarked.",
        CaptureSlot.ConstructionDetail => "Seams, linings or darts — anything that shows how it was made.",
        _ => "Any other angle worth keeping.",
    };

    /// <summary>Ordered slots for the capture UI: required first, then requested, then the rest.</summary>
    public static IEnumerable<CaptureSlot> AllSlotsInOrder() =>
        RequiredSlots
            .Concat(RequestedSlots)
            .Concat(Enum.GetValues<CaptureSlot>()
                .Except(RequiredSlots)
                .Except(RequestedSlots)
                .Where(s => s != CaptureSlot.Unspecified));
}
