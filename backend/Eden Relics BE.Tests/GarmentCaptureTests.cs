using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Exercises the capture pipeline against a real (in-memory) database and the real
/// ImageSharp decode path, with object storage unconfigured so files land on disk in a
/// temp web root.
/// </summary>
public class GarmentCaptureTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly string _webRoot;

    public GarmentCaptureTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), "er-capture-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_webRoot);

        // Name the database ONCE. The options lambda runs per DbContext construction, so
        // generating the name inside it gives every scope its own empty database.
        string dbName = $"capture-{Guid.NewGuid():N}";

        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<EdenRelicsDbContext>(o =>
            o.UseInMemoryDatabase(dbName)
             .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddSingleton<IWebHostEnvironment>(new TestEnv(_webRoot));
        services.AddSingleton(new ImageStorageService(
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<ImageStorageService>.Instance));
        services.AddScoped<IGarmentCaptureService, GarmentCaptureService>();

        _provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Only the two paths are ever read by the service; the file providers exist to satisfy
    /// the interface and are deliberately not wired up.
    /// </summary>
    private sealed class TestEnv(string root) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(root);
        public string ApplicationName { get; set; } = "tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
        public string ContentRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Testing";
    }

    private async Task<Guid> SeedGarmentAsync()
    {
        using IServiceScope scope = _provider.CreateScope();
        IRepository<Garment> garments = scope.ServiceProvider.GetRequiredService<IRepository<Garment>>();
        Garment garment = new() { Id = Guid.CreateVersion7(), Reference = "TEST-1" };
        await garments.AddAsync(garment);
        return garment.Id;
    }

    /// <summary>A solid-colour image of the requested size, encoded as real bytes.</summary>
    private static MemoryStream MakeImage(int width, int height, bool png = false)
    {
        using Image<Rgba32> image = new(width, height);
        MemoryStream ms = new();
        if (png)
        {
            image.SaveAsPng(ms, new PngEncoder());
        }
        else
        {
            image.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
        }
        ms.Position = 0;
        return ms;
    }

    private IGarmentCaptureService Service(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IGarmentCaptureService>();

    [Fact]
    public async Task Capture_StoresArchiveAndDisplay_AndRecordsDimensions()
    {
        Guid garmentId = await SeedGarmentAsync();
        using IServiceScope scope = _provider.CreateScope();

        using MemoryStream image = MakeImage(2000, 1500);
        CaptureResult result = await Service(scope).CaptureAsync(
            garmentId, CaptureSlot.CareLabel, image, "image/jpeg", image.Length, archiveRightsGranted: true);

        Assert.True(result.Succeeded);
        GarmentCapture capture = result.Capture!;
        Assert.Equal(2000, capture.Width);
        Assert.Equal(1500, capture.Height);
        Assert.NotNull(capture.DisplayUrl);
        Assert.EndsWith(".jpg", capture.ArchiveUrl);
        Assert.EndsWith(".webp", capture.DisplayUrl);
        Assert.Equal(CaptureStandard.Version, capture.ArchiveTermsVersion);
    }

    /// <summary>
    /// The archive copy must be the uploaded bytes exactly. Detail discarded at capture
    /// time cannot be recovered, and this archive is what recognition gets trained on.
    /// </summary>
    [Fact]
    public async Task Capture_ArchivesOriginalBytesVerbatim()
    {
        Guid garmentId = await SeedGarmentAsync();
        using IServiceScope scope = _provider.CreateScope();

        using MemoryStream image = MakeImage(2000, 1500);
        byte[] original = image.ToArray();
        image.Position = 0;

        CaptureResult result = await Service(scope).CaptureAsync(
            garmentId, CaptureSlot.CareLabel, image, "image/jpeg", image.Length, archiveRightsGranted: true);

        Assert.True(result.Succeeded);

        string relative = result.Capture!.ArchiveUrl.Replace("/uploads/", "").Replace('/', Path.DirectorySeparatorChar);
        string path = Path.Combine(_webRoot, "uploads", relative);
        byte[] stored = await File.ReadAllBytesAsync(path);

        Assert.Equal(original, stored);
    }

    /// <summary>
    /// The resolution floor is the whole point of a "fixed" standard — a 400px care label
    /// costs nothing to accept today and quietly ruins the archive.
    /// </summary>
    [Fact]
    public async Task Capture_RejectsUndersizedLabelPhoto()
    {
        Guid garmentId = await SeedGarmentAsync();
        using IServiceScope scope = _provider.CreateScope();

        using MemoryStream image = MakeImage(600, 400);
        CaptureResult result = await Service(scope).CaptureAsync(
            garmentId, CaptureSlot.CareLabel, image, "image/jpeg", image.Length, archiveRightsGranted: true);

        Assert.False(result.Succeeded);
        Assert.Equal("too_low_resolution", result.Rejection!.Code);
    }

    /// <summary>Detail slots are allowed to be smaller than label slots.</summary>
    [Fact]
    public async Task Capture_AppliesPerSlotResolutionFloors()
    {
        Guid garmentId = await SeedGarmentAsync();
        using IServiceScope scope = _provider.CreateScope();

        // 1100px: below the 1200 label floor, above the 1000 zip floor.
        using MemoryStream tooSmallForLabel = MakeImage(1100, 800);
        Assert.False((await Service(scope).CaptureAsync(
            garmentId, CaptureSlot.BrandLabel, tooSmallForLabel, "image/jpeg",
            tooSmallForLabel.Length, true)).Succeeded);

        using MemoryStream fineForZip = MakeImage(1100, 800);
        Assert.True((await Service(scope).CaptureAsync(
            garmentId, CaptureSlot.Zip, fineForZip, "image/jpeg", fineForZip.Length, true)).Succeeded);
    }

    /// <summary>
    /// Rights are per capture. Storing an image first and resolving the paperwork later
    /// would leave unusable material in the one asset that has to be unencumbered.
    /// </summary>
    [Fact]
    public async Task Capture_RefusesWithoutArchiveRights()
    {
        Guid garmentId = await SeedGarmentAsync();
        using IServiceScope scope = _provider.CreateScope();

        using MemoryStream image = MakeImage(2000, 1500);
        CaptureResult result = await Service(scope).CaptureAsync(
            garmentId, CaptureSlot.CareLabel, image, "image/jpeg", image.Length, archiveRightsGranted: false);

        Assert.False(result.Succeeded);
        Assert.Equal("rights_not_granted", result.Rejection!.Code);
        Assert.Empty(Directory.Exists(_webRoot) ? Directory.GetFiles(_webRoot, "*", SearchOption.AllDirectories) : []);
    }

    [Fact]
    public async Task Capture_RejectsUnsupportedFormat()
    {
        Guid garmentId = await SeedGarmentAsync();
        using IServiceScope scope = _provider.CreateScope();

        using MemoryStream image = MakeImage(2000, 1500);
        CaptureResult result = await Service(scope).CaptureAsync(
            garmentId, CaptureSlot.CareLabel, image, "image/heic", image.Length, true);

        Assert.False(result.Succeeded);
        Assert.Equal("unsupported_format", result.Rejection!.Code);
    }

    [Fact]
    public async Task Capture_RejectsNonImageBytes()
    {
        Guid garmentId = await SeedGarmentAsync();
        using IServiceScope scope = _provider.CreateScope();

        using MemoryStream notAnImage = new("this is not a jpeg"u8.ToArray());
        CaptureResult result = await Service(scope).CaptureAsync(
            garmentId, CaptureSlot.CareLabel, notAnImage, "image/jpeg", notAnImage.Length, true);

        Assert.False(result.Succeeded);
        Assert.Equal("unreadable", result.Rejection!.Code);
    }

    [Fact]
    public async Task Capture_RejectsUnknownGarment()
    {
        using IServiceScope scope = _provider.CreateScope();
        using MemoryStream image = MakeImage(2000, 1500);

        CaptureResult result = await Service(scope).CaptureAsync(
            Guid.CreateVersion7(), CaptureSlot.CareLabel, image, "image/jpeg", image.Length, true);

        Assert.False(result.Succeeded);
        Assert.Equal("garment_not_found", result.Rejection!.Code);
    }

    /// <summary>
    /// A missing brand label must never block completeness — much real vintage stock has it
    /// cut out, and requiring it would push sellers into photographing the wrong thing.
    /// </summary>
    [Fact]
    public async Task Completeness_DoesNotRequireBrandLabel()
    {
        Guid garmentId = await SeedGarmentAsync();
        using IServiceScope scope = _provider.CreateScope();
        IGarmentCaptureService service = Service(scope);

        using MemoryStream care = MakeImage(2000, 1500);
        await service.CaptureAsync(garmentId, CaptureSlot.CareLabel, care, "image/jpeg", care.Length, true);
        using MemoryStream flat = MakeImage(2000, 1500);
        await service.CaptureAsync(garmentId, CaptureSlot.FlatLayFront, flat, "image/jpeg", flat.Length, true);

        CaptureCompleteness completeness = await service.GetCompletenessAsync(garmentId);

        Assert.True(completeness.IsComplete);
        Assert.Empty(completeness.MissingRequired);
        // Still asked for, just not demanded.
        Assert.Contains(CaptureSlot.BrandLabel, completeness.MissingRequested);
        Assert.Equal(2, completeness.CaptureCount);
    }

    [Fact]
    public async Task Completeness_ReportsMissingRequiredSlots()
    {
        Guid garmentId = await SeedGarmentAsync();
        using IServiceScope scope = _provider.CreateScope();
        IGarmentCaptureService service = Service(scope);

        using MemoryStream care = MakeImage(2000, 1500);
        await service.CaptureAsync(garmentId, CaptureSlot.CareLabel, care, "image/jpeg", care.Length, true);

        CaptureCompleteness completeness = await service.GetCompletenessAsync(garmentId);

        Assert.False(completeness.IsComplete);
        Assert.Contains(CaptureSlot.FlatLayFront, completeness.MissingRequired);
    }

    [Fact]
    public async Task Capture_AcceptsPng()
    {
        Guid garmentId = await SeedGarmentAsync();
        using IServiceScope scope = _provider.CreateScope();

        using MemoryStream image = MakeImage(1600, 1600, png: true);
        CaptureResult result = await Service(scope).CaptureAsync(
            garmentId, CaptureSlot.FlatLayFront, image, "image/png", image.Length, true);

        Assert.True(result.Succeeded);
        Assert.EndsWith(".png", result.Capture!.ArchiveUrl);
    }

    public void Dispose()
    {
        _provider.Dispose();
        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
        GC.SuppressFinalize(this);
    }
}
