using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Eden_Relics_BE.Services;

public static class ImageUploadHelper
{
    public static async Task<string> ProcessAndUploadAsync(
        Stream fileStream,
        ImageStorageService storage,
        IWebHostEnvironment env,
        HttpRequest request,
        string folder = "",
        int maxWidth = 800,
        int maxHeight = 1000,
        int quality = 75)
    {
        using var image = await Image.LoadAsync(fileStream);
        image.Mutate(x => x.AutoOrient());

        if (image.Width > maxWidth || image.Height > maxHeight)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxWidth, maxHeight),
                Mode = ResizeMode.Max,
            }));
        }

        string fileName = $"{(string.IsNullOrEmpty(folder) ? "" : folder + "/")}{Guid.NewGuid()}.webp";

        using MemoryStream output = new();
        await image.SaveAsync(output, new WebpEncoder { Quality = quality });
        output.Position = 0;

        if (storage.IsConfigured)
        {
            return await storage.UploadAsync(output, fileName, "image/webp");
        }

        // Fallback to local filesystem
        string uploadsDir = Path.Combine(
            env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
            "uploads",
            folder);
        Directory.CreateDirectory(uploadsDir);

        string localFileName = Path.GetFileName(fileName);
        string filePath = Path.Combine(uploadsDir, localFileName);
        await using FileStream fs = new(filePath, FileMode.Create);
        await output.CopyToAsync(fs);

        string pathSegment = string.IsNullOrEmpty(folder) ? localFileName : $"{folder}/{localFileName}";
        return $"{request.Scheme}://{request.Host}/uploads/{pathSegment}";
    }
}
