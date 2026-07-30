using Eden_Relics_BE.Data.Entities;

namespace Eden_Relics_BE.Services;

/// <summary>Outcome of attempting one capture. Rejection is a normal result, not an exception.</summary>
public record CaptureResult(GarmentCapture? Capture, CaptureRejection? Rejection)
{
    public bool Succeeded => Capture is not null;
}

/// <summary>
/// Photo capture into the owned label archive, to the fixed standard.
/// </summary>
public interface IGarmentCaptureService
{
    /// <summary>
    /// Validates an uploaded photograph against <see cref="CaptureStandard"/>, stores the
    /// original verbatim plus a web derivative, and records it against the garment.
    /// </summary>
    Task<CaptureResult> CaptureAsync(
        Guid garmentId,
        CaptureSlot slot,
        Stream content,
        string contentType,
        long byteSize,
        bool archiveRightsGranted,
        string notes = "",
        CancellationToken ct = default);

    /// <summary>Progress against the standard, for prompting the seller.</summary>
    Task<CaptureCompleteness> GetCompletenessAsync(Guid garmentId, CancellationToken ct = default);

    Task<IReadOnlyList<GarmentCapture>> GetForGarmentAsync(Guid garmentId, CancellationToken ct = default);
}
