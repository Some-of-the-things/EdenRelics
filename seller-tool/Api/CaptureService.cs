using EdenRelics.SellerTool.Data;
using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
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
        ImageProvenance provenance = ImageProvenance.LiveCapture,
        ZipOriginality? zipOriginality = null,
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
        ImageProvenance provenance = ImageProvenance.LiveCapture,
        ZipOriginality? zipOriginality = null,
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
        DateTime? photographedAt;

        await ProcessingGate.WaitAsync(ct);
        try
        {
            using Image image = await Image.LoadAsync(buffer, ct);
            // Before AutoOrient: it rewrites the EXIF orientation tag, and there is no reason to
            // read metadata from an image we have already started mutating.
            photographedAt = ReadPhotographedAt(image);
            image.Mutate(x => x.AutoOrient());
            width = image.Width;
            height = image.Height;

            // The standard is for photographs taken TO it. Holding the back catalogue to it would
            // reject most of the archive we are trying to seed — those photos are already taken and
            // cannot be retaken, the garments are long gone, and a blurry record of a 1975 care
            // label is worth incomparably more than no record. They are kept, flagged, and excluded
            // from anything that needs standard-quality input.
            int longEdge = Math.Max(width, height);
            int required = CaptureStandard.MinimumLongEdge(slot);
            if (provenance == ImageProvenance.LiveCapture && longEdge < required)
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
            Provenance = provenance,
            PhotographedAtLocal = photographedAt,
            ZipOriginality = zipOriginality,
            Origin = "capture",
            Confirmation = ConfirmationState.Proposed,
        };

        db.EvidenceRecords.Add(evidence);
        await db.SaveChangesAsync(ct);

        return new CaptureOutcome(evidence, null);
    }

    /// <summary>
    /// The photograph's own date, from EXIF. Null when absent or unreadable, which is common:
    /// screenshots, re-encodes and anything that has been through a messaging app lose it.
    ///
    /// Three tags are tried in descending trustworthiness — when the shutter fired, when it was
    /// digitised, then the file's own stamp. Returned with Unspecified kind because that is the
    /// truth: EXIF has no timezone, so this is the camera's wall clock and calling it UTC would be
    /// inventing an hour or two of precision we do not have.
    /// </summary>
    private static DateTime? ReadPhotographedAt(Image image)
    {
        ExifProfile? exif = image.Metadata.ExifProfile;
        if (exif is null)
        {
            return null;
        }

        foreach (ExifTag<string> tag in new[] { ExifTag.DateTimeOriginal, ExifTag.DateTimeDigitized, ExifTag.DateTime })
        {
            if (exif.TryGetValue(tag, out IExifValue<string>? value)
                && !string.IsNullOrWhiteSpace(value?.Value)
                && DateTime.TryParseExact(
                    value.Value, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
            }
        }
        return null;
    }

    public async Task<CaptureCompleteness> GetCompletenessAsync(Guid garmentId, CancellationToken ct = default)
    {
        List<EvidenceRecord> captures = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(db.EvidenceRecords.Where(e =>
                e.GarmentId == garmentId
                && e.ImageKey != null
                // Completeness means "meets the capture standard", and a back-catalogue photo does
                // not, by definition. Counting one would report a garment as properly captured on
                // the strength of an old snapshot that was never held to the standard at all.
                && e.Provenance == ImageProvenance.LiveCapture), ct);

        HashSet<CaptureSlot> present = captures.Select(c => c.Slot).ToHashSet();
        List<CaptureSlot> missingRequired = CaptureStandard.RequiredSlots.Except(present).ToList();
        List<CaptureSlot> missingRequested = CaptureStandard.RequestedSlots.Except(present).ToList();

        return new CaptureCompleteness(
            missingRequired.Count == 0, missingRequired, missingRequested, captures.Count);
    }

    private static CaptureOutcome Reject(string code, string message) =>
        new(null, new CaptureRejection(code, message));
}
