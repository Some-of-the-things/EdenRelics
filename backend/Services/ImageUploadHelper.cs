using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Eden_Relics_BE.Services;

public static class ImageUploadHelper
{
    public static readonly int[] DefaultVariantWidths = [400, 800, 1200, 1600];

    // Bound concurrent image processing across the whole process. ImageSharp decodes the
    // full bitmap into memory (~48 MB for a 4000x3000 phone photo); without this cap,
    // a multi-image admin upload or a backfill colliding with live traffic OOMs the box.
    public static readonly SemaphoreSlim ProcessingGate = new(2, 2);

    public static async Task<string> ProcessAndUploadAsync(
        Stream fileStream,
        ImageStorageService storage,
        IWebHostEnvironment env,
        HttpRequest request,
        string folder = "",
        int[]? variantWidths = null,
        int quality = 75)
    {
        int[] widths = variantWidths ?? DefaultVariantWidths;
        if (widths.Length == 0)
        {
            throw new ArgumentException("At least one variant width is required.", nameof(variantWidths));
        }

        await ProcessingGate.WaitAsync();
        try
        {
            return await ProcessAndUploadCoreAsync(fileStream, storage, env, request, folder, widths, quality);
        }
        finally
        {
            ProcessingGate.Release();
        }
    }

    private static async Task<string> ProcessAndUploadCoreAsync(
        Stream fileStream,
        ImageStorageService storage,
        IWebHostEnvironment env,
        HttpRequest request,
        string folder,
        int[] widths,
        int quality)
    {
        using Image source = await Image.LoadAsync(fileStream);
        source.Mutate(x => x.AutoOrient());

        string baseName = Guid.NewGuid().ToString();
        string folderPrefix = string.IsNullOrEmpty(folder) ? "" : folder + "/";
        int largestWidth = widths.Max();
        string canonicalUrl = "";

        // Process largest-to-smallest, mutating the source down through each size.
        // A 4000x3000 phone photo decodes to ~48 MB; cloning per variant pushed peak
        // memory above the 512 MB instance cap and OOM-killed the process during uploads.
        int[] orderedWidths = widths.OrderByDescending(w => w).ToArray();
        foreach (int targetWidth in orderedWidths)
        {
            if (source.Width > targetWidth)
            {
                source.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth, 0),
                    Mode = ResizeMode.Max,
                }));
            }

            string fileName = $"{folderPrefix}{baseName}-{targetWidth}.webp";
            using MemoryStream output = new();
            await source.SaveAsync(output, new WebpEncoder { Quality = quality });
            output.Position = 0;

            string url = await StoreAsync(output, fileName, storage, env, request, folder);
            if (targetWidth == largestWidth)
            {
                canonicalUrl = url;
            }
        }

        return canonicalUrl;
    }

    public static async Task<string> ProcessAndUploadSingleAsync(
        Stream fileStream,
        ImageStorageService storage,
        IWebHostEnvironment env,
        HttpRequest request,
        string folder,
        int maxWidth,
        int maxHeight,
        int quality)
    {
        await ProcessingGate.WaitAsync();
        try
        {
            using Image image = await Image.LoadAsync(fileStream);
            image.Mutate(x => x.AutoOrient());

            if (image.Width > maxWidth || image.Height > maxHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxWidth, maxHeight),
                    Mode = ResizeMode.Max,
                }));
            }

            string folderPrefix = string.IsNullOrEmpty(folder) ? "" : folder + "/";
            string fileName = $"{folderPrefix}{Guid.NewGuid()}.webp";

            using MemoryStream output = new();
            await image.SaveAsync(output, new WebpEncoder { Quality = quality });
            output.Position = 0;

            return await StoreAsync(output, fileName, storage, env, request, folder);
        }
        finally
        {
            ProcessingGate.Release();
        }
    }

    private static async Task<string> StoreAsync(
        MemoryStream content,
        string fileName,
        ImageStorageService storage,
        IWebHostEnvironment env,
        HttpRequest request,
        string folder)
    {
        if (storage.IsConfigured)
        {
            return await storage.UploadAsync(content, fileName, "image/webp");
        }

        string uploadsDir = Path.Combine(
            env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
            "uploads",
            folder);
        Directory.CreateDirectory(uploadsDir);

        string localFileName = Path.GetFileName(fileName);
        string filePath = Path.Combine(uploadsDir, localFileName);
        await using FileStream fs = new(filePath, FileMode.Create);
        await content.CopyToAsync(fs);

        string pathSegment = string.IsNullOrEmpty(folder) ? localFileName : $"{folder}/{localFileName}";
        return $"{request.Scheme}://{request.Host}/uploads/{pathSegment}";
    }
}
