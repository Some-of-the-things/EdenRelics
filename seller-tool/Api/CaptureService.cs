using EdenRelics.SellerTool.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace EdenRelics.SellerTool.Api;

/// <summary>Why a capture was refused, in a form the UI can show the seller.</summary>
public sealed record CaptureRejection(string Code, string Message);

/// <summary>A stored capture, or the reason it was refused. Never both.</summary>
public sealed record CaptureOutcome(EvidenceRecord? Evidence, CaptureRejection? Rejection)
{
    public bool Succeeded => Rejection is null;
}

/// <summary>What is still missing before a garment meets the capture standard.</summary>
public sealed record CaptureCompleteness(
    bool IsComplete,
    IReadOnlyList<CaptureSlot> MissingRequired,
    IReadOnlyList<CaptureSlot> MissingRequested,
    int CaptureCount);

public interface ICaptureService
{
    Task<CaptureOutcome> CaptureAsync(
        Guid garmentId,
        CaptureSlot slot,
        Dating.EvidenceType type,
        string feature,
        Stream content,
        string contentType,
        long byteSize,
        bool archiveRightsGranted,
        CancellationToken ct = default);

    Task<CaptureCompleteness> GetCompletenessAsync(Guid garmentId, CancellationToken ct = default);
}

/// <summary>
/// Photo capture into the owned label archive.
///
/// The archive is the part competitors cannot fast-follow, so this validates BEFORE storing rather
/// than storing and sorting it out later: an unreadable or undersized image accepted today is a gap
/// in the corpus that only shows up years later, when something tries to derive a rule from it.
/// </summary>
public sealed class CaptureService(ToolDbContext db, IImageStore images) : ICaptureService
{
    /// <summary>
    /// ImageSharp decodes the whole bitmap, so concurrent uploads are capped — the tool runs on a
    /// small Fly machine and a handful of 25MB photos decoding at once is enough to exhaust it.
    /// </summary>
    private static readonly SemaphoreSlim ProcessingGate = new(2, 2);

    public async Task<CaptureOutcome> CaptureAsync(
        Guid garmentId,
        CaptureSlot slot,
        Dating.EvidenceType type,
        string feature,
        Stream content,
        string contentType,
        long byteSize,
        bool archiveRightsGranted,
        CancellationToken ct = default)
    {
        // Rights are recorded per capture, so a capture without them must not reach the archive at
        // all. Storing first and sorting the paperwork out later would leave images we cannot
        // legitimately use in the one asset that has to be unencumbered.
        if (!archiveRightsGranted)
        {
            return Reject("rights_not_granted",
                "Archive rights must be granted at capture time for the image to be stored.");
        }

        if (!CaptureStandard.AcceptedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return Reject("unsupported_format", $"{contentType} is not accepted. Use JPEG, PNG or WebP.");
        }

        if (byteSize <= 0 || byteSize > CaptureStandard.MaxBytes)
        {
            return Reject("too_large", $"Images must be under {CaptureStandard.MaxBytes / (1024 * 1024)} MB.");
        }

        // Buffer once: the bytes are needed twice — decoded to measure and validate, then stored
        // verbatim. The request stream is forward-only.
        using MemoryStream buffer = new();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        if (buffer.Length > CaptureStandard.MaxBytes)
        {
            return Reject("too_large", $"Images must be under {CaptureStandard.MaxBytes / (1024 * 1024)} MB.");
        }
        byteSize = buffer.Length;

        int width;
        int height;
        byte[] displayBytes;

        await ProcessingGate.WaitAsync(ct);
        try
        {
            using Image image = await Image.LoadAsync(buffer, ct);
            image.Mutate(x => x.AutoOrient());
            width = image.Width;
            height = image.Height;

            int longEdge = Math.Max(width, height);
            int required = CaptureStandard.MinimumLongEdge(slot);
            if (longEdge < required)
            {
                return Reject("too_low_resolution",
                    $"A {slot} photo needs at least {required}px on its longest edge; this one is {longEdge}px. "
                    + "Move closer and fill the frame rather than cropping afterwards.");
            }

            using Image derivative = image.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new Size(CaptureStandard.DisplayLongEdge, CaptureStandard.DisplayLongEdge),
                Mode = ResizeMode.Max,
            }));
            using MemoryStream output = new();
            await derivative.SaveAsync(output, new WebpEncoder { Quality = CaptureStandard.DisplayQuality }, ct);
            displayBytes = output.ToArray();
        }
        catch (UnknownImageFormatException)
        {
            return Reject("unreadable", "That file could not be read as an image.");
        }
        catch (InvalidImageContentException)
        {
            return Reject("unreadable", "That image appears to be corrupt.");
        }
        finally
        {
            ProcessingGate.Release();
        }

        // The original goes in untouched. Everything else is derived and disposable.
        buffer.Position = 0;
        string archiveKey = await images.PutAsync(buffer, contentType, $"garments/{garmentId:N}/archive", ct);

        using MemoryStream displayStream = new(displayBytes);
        string displayKey = await images.PutAsync(displayStream, "image/webp", $"garments/{garmentId:N}/display", ct);

        EvidenceRecord evidence = new()
        {
            GarmentId = garmentId,
            Type = type,
            Feature = feature,
            Slot = slot,
            ImageKey = archiveKey,
            DisplayImageKey = displayKey,
            ArchiveRightsGranted = true,
            CaptureStandardVersion = CaptureStandard.Version,
            Width = width,
            Height = height,
            ByteSize = byteSize,
            ContentType = contentType,
            Origin = "capture",
            Confirmation = ConfirmationState.Proposed,
        };

        db.EvidenceRecords.Add(evidence);
        await db.SaveChangesAsync(ct);

        return new CaptureOutcome(evidence, null);
    }

    public async Task<CaptureCompleteness> GetCompletenessAsync(Guid garmentId, CancellationToken ct = default)
    {
        List<EvidenceRecord> captures = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(db.EvidenceRecords.Where(e => e.GarmentId == garmentId && e.ImageKey != null), ct);

        HashSet<CaptureSlot> present = captures.Select(c => c.Slot).ToHashSet();
        List<CaptureSlot> missingRequired = CaptureStandard.RequiredSlots.Except(present).ToList();
        List<CaptureSlot> missingRequested = CaptureStandard.RequestedSlots.Except(present).ToList();

        return new CaptureCompleteness(
            missingRequired.Count == 0, missingRequired, missingRequested, captures.Count);
    }

    private static CaptureOutcome Reject(string code, string message) =>
        new(null, new CaptureRejection(code, message));
}
