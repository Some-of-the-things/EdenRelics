namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// Which photograph in the capture standard this image is. Fixed set on purpose: the
/// archive is only worth anything if every garment is photographed the same way, so the
/// slots are a closed vocabulary rather than a free-text caption.
/// </summary>
public enum CaptureSlot
{
    Other = 0,
    /// <summary>The maker's/brand label. Often absent — that is expected, not an error.</summary>
    BrandLabel = 1,
    /// <summary>The care label, including symbols and any printed address or phone number.</summary>
    CareLabel = 2,
    /// <summary>Whole garment, flat, front. The measurement source.</summary>
    FlatLayFront = 3,
    FlatLayBack = 4,
    /// <summary>Seams, linings, darts — construction tells that carry dating evidence.</summary>
    ConstructionDetail = 5,
    /// <summary>The zip: teeth material, puller stamp, tape.</summary>
    Zip = 6,
    /// <summary>A specific flaw, for honest condition reporting.</summary>
    Flaw = 7,
}

/// <summary>
/// One photograph captured against the standard, and the archive record for it.
///
/// This is the flywheel. Every listing made through the tool captures brand and care
/// labels to a fixed standard into an archive we own — invisible effort to the seller,
/// and the asset that eventually powers brand recognition and insurable dating. It is
/// worth capturing before anything consumes it, because a day not captured is label data
/// that cannot be recovered later.
/// </summary>
public class GarmentCapture : BaseEntity
{
    public Guid GarmentId { get; set; }
    public Garment? Garment { get; set; }

    public CaptureSlot Slot { get; set; }

    /// <summary>
    /// The uploaded bytes stored verbatim — no resize, no re-encode.
    ///
    /// Deliberately NOT the optimised web image. Recognition will eventually be trained on
    /// this archive, and detail thrown away at capture time cannot be recovered. A care
    /// symbol or a stamped zip puller survives in the original and may not survive a
    /// downscale-and-recompress.
    /// </summary>
    public required string ArchiveUrl { get; set; }

    /// <summary>Web-sized derivative for the listing UI. Cheap to regenerate from the archive.</summary>
    public string? DisplayUrl { get; set; }

    public string ContentType { get; set; } = "";
    public long ByteSize { get; set; }

    /// <summary>Pixel dimensions of the original, after orientation is normalised.</summary>
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>
    /// Whether the seller granted archive rights at the point of capture, and under which
    /// version of the terms. Recorded per capture rather than per account: the archive is
    /// the long-term asset, and its provenance has to survive a seller later leaving or
    /// the terms changing.
    /// </summary>
    public bool ArchiveRightsGranted { get; set; }
    public string? ArchiveTermsVersion { get; set; }

    public DateTime CapturedAtUtc { get; set; }

    /// <summary>Free-text note from the seller — what this shows, where on the garment.</summary>
    public string Notes { get; set; } = "";

    /// <summary>Evidence read out of this photograph, if any has been recorded yet.</summary>
    public List<GarmentEvidence> Evidence { get; set; } = [];
}
