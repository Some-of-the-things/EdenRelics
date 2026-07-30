using Eden_Relics_BE.Auth;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

/// <summary>
/// Photo capture into the owned label archive.
///
/// Admin-only for now. The seller tool is pre-beta and the rules engine is not yet
/// trustworthy enough to show anyone — but capture is worth running from day one, because
/// a day not captured is label data that cannot be recovered later. Eden Relics is beta
/// tester zero, so the shop's own listings are what fill the archive first.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/garments")]
public class GarmentCaptureController(
    IGarmentCaptureService capture,
    ILogger<GarmentCaptureController> logger) : ControllerBase
{
    public record CaptureStandardSlotDto(string Slot, bool Required, int MinimumLongEdge, string Guidance);

    public record CaptureDto(
        Guid Id,
        string Slot,
        string ArchiveUrl,
        string? DisplayUrl,
        int Width,
        int Height,
        long ByteSize,
        DateTime CapturedAtUtc,
        string Notes);

    /// <summary>
    /// The capture standard itself, so the client renders the slots and their guidance from
    /// one definition rather than duplicating it.
    /// </summary>
    [HttpGet("capture-standard")]
    public ActionResult<object> Standard()
    {
        IEnumerable<CaptureSlot> slots = CaptureStandard.RequiredSlots
            .Concat(CaptureStandard.RequestedSlots)
            .Concat(Enum.GetValues<CaptureSlot>()
                .Except(CaptureStandard.RequiredSlots)
                .Except(CaptureStandard.RequestedSlots));

        return Ok(new
        {
            version = CaptureStandard.Version,
            maxBytes = CaptureStandard.MaxBytes,
            acceptedContentTypes = CaptureStandard.AcceptedContentTypes,
            slots = slots.Select(s => new CaptureStandardSlotDto(
                s.ToString(),
                CaptureStandard.RequiredSlots.Contains(s),
                CaptureStandard.MinimumLongEdge(s),
                CaptureStandard.Guidance(s))),
        });
    }

    /// <summary>
    /// Uploads one photograph against a slot. <paramref name="archiveRights"/> must be true:
    /// rights are recorded per capture, not per account, so the archive's provenance
    /// survives a seller leaving or the terms changing.
    /// </summary>
    [HttpPost("{garmentId:guid}/captures")]
    [RequestSizeLimit(CaptureStandard.MaxBytes)]
    public async Task<IActionResult> Upload(
        Guid garmentId,
        [FromForm] IFormFile file,
        [FromForm] CaptureSlot slot,
        [FromForm] bool archiveRights,
        [FromForm] string? notes,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { code = "empty", message = "No file was uploaded." });
        }

        await using Stream stream = file.OpenReadStream();
        CaptureResult result = await capture.CaptureAsync(
            garmentId,
            slot,
            stream,
            file.ContentType ?? "",
            file.Length,
            archiveRights,
            notes ?? "",
            ct);

        if (!result.Succeeded)
        {
            // A rejection is an expected outcome — a blurry or undersized label is the normal
            // case the standard exists to catch — so it returns a reason the UI can show.
            logger.LogInformation(
                "Capture rejected for garment {GarmentId}: {Code}", garmentId, result.Rejection!.Code);
            return BadRequest(new { code = result.Rejection.Code, message = result.Rejection.Message });
        }

        return Ok(ToDto(result.Capture!));
    }

    [HttpGet("{garmentId:guid}/captures")]
    public async Task<ActionResult<IEnumerable<CaptureDto>>> List(Guid garmentId, CancellationToken ct)
    {
        IReadOnlyList<GarmentCapture> items = await capture.GetForGarmentAsync(garmentId, ct);
        return Ok(items.Select(ToDto));
    }

    /// <summary>What is still missing before this garment meets the standard.</summary>
    [HttpGet("{garmentId:guid}/captures/completeness")]
    public async Task<ActionResult<object>> Completeness(Guid garmentId, CancellationToken ct)
    {
        CaptureCompleteness result = await capture.GetCompletenessAsync(garmentId, ct);
        return Ok(new
        {
            isComplete = result.IsComplete,
            captureCount = result.CaptureCount,
            missingRequired = result.MissingRequired.Select(s => s.ToString()),
            missingRequested = result.MissingRequested.Select(s => s.ToString()),
        });
    }

    private static CaptureDto ToDto(GarmentCapture c) => new(
        c.Id, c.Slot.ToString(), c.ArchiveUrl, c.DisplayUrl,
        c.Width, c.Height, c.ByteSize, c.CapturedAtUtc, c.Notes);
}
