using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Eden_Relics_BE.Services;

/// <inheritdoc />
public class GarmentCaptureService(
    IRepository<Garment> garments,
    IRepository<GarmentCapture> captures,
    ImageStorageService storage,
    IWebHostEnvironment env,
    ILogger<GarmentCaptureService> logger) : IGarmentCaptureService
{
    public async Task<CaptureResult> CaptureAsync(
        Guid garmentId,
        CaptureSlot slot,
        Stream content,
        string contentType,
        long byteSize,
        bool archiveRightsGranted,
        string notes = "",
        CancellationToken ct = default)
    {
        // Rights are recorded per capture, so a capture without them must not reach the
        // archive at all. Storing first and sorting the paperwork out later would leave
        // images we cannot legitimately use in the one asset that has to be unencumbered.
        if (!archiveRightsGranted)
        {
            return Reject("rights_not_granted",
                "Archive rights must be granted at capture time for the image to be stored.");
        }

        if (!CaptureStandard.AcceptedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return Reject("unsupported_format",
                $"{contentType} is not accepted. Use JPEG, PNG or WebP.");
        }

        if (byteSize <= 0 || byteSize > CaptureStandard.MaxBytes)
        {
            return Reject("too_large",
                $"Images must be under {CaptureStandard.MaxBytes / (1024 * 1024)} MB.");
        }

        bool garmentExists = await garments.Query().AnyAsync(g => g.Id == garmentId, ct);
        if (!garmentExists)
        {
            return Reject("garment_not_found", "No such garment.");
        }

        // Buffer once: the bytes are needed twice — decoded to measure and validate, then
        // stored verbatim. The request stream is forward-only.
        using MemoryStream buffer = new();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        if (buffer.Length != byteSize)
        {
            byteSize = buffer.Length;
            if (byteSize > CaptureStandard.MaxBytes)
            {
                return Reject("too_large",
                    $"Images must be under {CaptureStandard.MaxBytes / (1024 * 1024)} MB.");
            }
        }

        int width;
        int height;
        byte[] displayBytes;

        // ImageSharp decodes the whole bitmap; the shared gate keeps concurrent uploads
        // from pushing the instance over its memory cap.
        await ImageUploadHelper.ProcessingGate.WaitAsync(ct);
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

            displayBytes = await BuildDisplayDerivativeAsync(image, ct);
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
            ImageUploadHelper.ProcessingGate.Release();
        }

        string baseName = $"{garmentId:N}/{Guid.CreateVersion7():N}";

        // The original goes in untouched. Everything else is derived and disposable.
        buffer.Position = 0;
        string archiveUrl = await StoreAsync(
            buffer, $"{baseName}-archive{ExtensionFor(contentType)}", contentType);

        using MemoryStream displayStream = new(displayBytes);
        string displayUrl = await StoreAsync(displayStream, $"{baseName}-display.webp", "image/webp");

        GarmentCapture capture = new()
        {
            GarmentId = garmentId,
            Slot = slot,
            ArchiveUrl = archiveUrl,
            DisplayUrl = displayUrl,
            ContentType = contentType,
            ByteSize = byteSize,
            Width = width,
            Height = height,
            ArchiveRightsGranted = true,
            ArchiveTermsVersion = CaptureStandard.Version,
            CapturedAtUtc = DateTime.UtcNow,
            Notes = notes,
        };

        await captures.AddAsync(capture);

        logger.LogInformation(
            "Captured {Slot} for garment {GarmentId}: {Width}x{Height}, {Bytes} bytes archived",
            slot, garmentId, width, height, byteSize);

        return new CaptureResult(capture, null);
    }

    public async Task<CaptureCompleteness> GetCompletenessAsync(Guid garmentId, CancellationToken ct = default)
    {
        List<CaptureSlot> present = await captures.Query()
            .Where(c => c.GarmentId == garmentId)
            .Select(c => c.Slot)
            .Distinct()
            .ToListAsync(ct);

        List<CaptureSlot> missingRequired = CaptureStandard.RequiredSlots.Except(present).ToList();
        List<CaptureSlot> missingRequested = CaptureStandard.RequestedSlots.Except(present).ToList();

        int count = await captures.Query().CountAsync(c => c.GarmentId == garmentId, ct);

        return new CaptureCompleteness(
            missingRequired.Count == 0,
            missingRequired,
            missingRequested,
            count);
    }

    public async Task<IReadOnlyList<GarmentCapture>> GetForGarmentAsync(
        Guid garmentId, CancellationToken ct = default) =>
        await captures.Query()
            .Where(c => c.GarmentId == garmentId)
            .OrderBy(c => c.Slot)
            .ThenBy(c => c.CapturedAtUtc)
            .ToListAsync(ct);

    private static async Task<byte[]> BuildDisplayDerivativeAsync(Image image, CancellationToken ct)
    {
        // Clone so the archive-side measurements above are not taken from a resized bitmap.
        using Image derivative = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(CaptureStandard.DisplayLongEdge, CaptureStandard.DisplayLongEdge),
            Mode = ResizeMode.Max,
        }));

        using MemoryStream output = new();
        await derivative.SaveAsync(output, new WebpEncoder { Quality = CaptureStandard.DisplayQuality }, ct);
        return output.ToArray();
    }

    private async Task<string> StoreAsync(MemoryStream content, string fileName, string contentType)
    {
        content.Position = 0;
        if (storage.IsConfigured)
        {
            return await storage.UploadAsync(content, $"garments/{fileName}", contentType);
        }

        // Local development fallback. Nested path, because captures are grouped per garment
        // rather than dumped in one directory.
        string root = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        string relative = Path.Combine("uploads", "garments", fileName.Replace('/', Path.DirectorySeparatorChar));
        string fullPath = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (FileStream fs = new(fullPath, FileMode.Create))
        {
            await content.CopyToAsync(fs);
        }

        // Root-relative rather than absolute: this path is only reached when object storage
        // is unconfigured (i.e. local dev), and a relative URL stays correct whatever host
        // the API is reached on, without needing the request in a service.
        return $"/uploads/garments/{fileName}";
    }

    private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg",
    };

    private static CaptureResult Reject(string code, string message) =>
        new(null, new CaptureRejection(code, message));
}
