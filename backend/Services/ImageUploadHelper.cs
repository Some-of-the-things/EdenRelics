using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Eden_Relics_BE.Services;

public static class ImageUploadHelper
{
    public static readonly int[] DefaultVariantWidths = [400, 800, 1200, 1600];

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

        using Image source = await Image.LoadAsync(fileStream);
        source.Mutate(x => x.AutoOrient());

        string baseName = Guid.NewGuid().ToString();
        string folderPrefix = string.IsNullOrEmpty(folder) ? "" : folder + "/";
        int largestWidth = widths.Max();
        string canonicalUrl = "";

        foreach (int targetWidth in widths)
        {
            using Image variant = source.Clone(_ => { });
            if (variant.Width > targetWidth)
            {
                variant.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth, 0),
                    Mode = ResizeMode.Max,
                }));
            }

            string fileName = $"{folderPrefix}{baseName}-{targetWidth}.webp";
            using MemoryStream output = new();
            await variant.SaveAsync(output, new WebpEncoder { Quality = quality });
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
