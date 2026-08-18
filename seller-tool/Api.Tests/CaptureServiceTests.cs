using EdenRelics.SellerTool.Api;
using EdenRelics.SellerTool.Data;
using EdenRelics.SellerTool.Dating;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace EdenRelics.SellerTool.Api.Tests;

/// <summary>
/// The capture standard exists to keep unusable images OUT of the archive. These assert the refusals
/// as hard as the successes — an undersized care label accepted today is a gap in the corpus that
/// only surfaces years later, when something tries to derive a rule from it.
/// </summary>
public class CaptureServiceTests
{
    private sealed class FakeImageStore : IImageStore
    {
        public List<(string ContentType, string Prefix, long Bytes)> Puts { get; } = [];

        public Task<string> PutAsync(Stream content, string contentType, string keyPrefix, CancellationToken ct = default)
        {
            Puts.Add((contentType, keyPrefix, content.Length));
            return Task.FromResult($"{keyPrefix}/{Guid.NewGuid():N}");
        }
    }

    private static (CaptureService Service, ToolDbContext Db, FakeImageStore Store) Build()
    {
        DbContextOptions<ToolDbContext> options = new DbContextOptionsBuilder<ToolDbContext>()
            .UseInMemoryDatabase($"capture-{Guid.NewGuid():N}")
            .Options;
        ToolDbContext db = new(options);
        FakeImageStore store = new();
        return (new CaptureService(db, store), db, store);
    }

    private static Guid SeedGarment(ToolDbContext db)
    {
        Garment g = new() { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Title = "test" };
        db.Garments.Add(g);
        db.SaveChanges();
        return g.Id;
    }

    private static MemoryStream Jpeg(int width, int height)
    {
        using Image<SixLabors.ImageSharp.PixelFormats.Rgba32> img = new(width, height);
        MemoryStream ms = new();
        img.Save(ms, new JpegEncoder());
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream Png(int width, int height)
    {
        using Image<SixLabors.ImageSharp.PixelFormats.Rgba32> img = new(width, height);
        MemoryStream ms = new();
        img.Save(ms, new PngEncoder());
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task StoresTheOriginalVerbatimAndGeneratesAWebDerivative()
    {
        (CaptureService svc, ToolDbContext db, FakeImageStore store) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream jpeg = Jpeg(2000, 1500);
        long originalLength = jpeg.Length;

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.CareLabel, EvidenceType.CareLabel, "care.wash-symbol",
            jpeg, "image/jpeg", jpeg.Length, archiveRightsGranted: true);

        Assert.True(outcome.Succeeded);
        EvidenceRecord e = outcome.Evidence!;
        Assert.Equal(2000, e.Width);
        Assert.Equal(1500, e.Height);
        Assert.Equal(originalLength, e.ByteSize);
        Assert.Equal(CaptureStandard.Version, e.CaptureStandardVersion);
        Assert.True(e.ArchiveRightsGranted);

        // Two objects: the untouched original, then a WebP derivative.
        Assert.Equal(2, store.Puts.Count);
        Assert.Equal("image/jpeg", store.Puts[0].ContentType);
        Assert.Equal(originalLength, store.Puts[0].Bytes);   // stored verbatim, not re-encoded
        Assert.Equal("image/webp", store.Puts[1].ContentType);
        Assert.NotNull(e.DisplayImageKey);
    }

    /// <summary>
    /// Rights are recorded per capture, not per account, so an ungranted upload must not reach
    /// storage at all — not be stored and reconciled later.
    /// </summary>
    [Fact]
    public async Task RefusesWithoutArchiveRights_AndStoresNothing()
    {
        (CaptureService svc, ToolDbContext db, FakeImageStore store) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream jpeg = Jpeg(2000, 1500);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.CareLabel, EvidenceType.CareLabel, "f",
            jpeg, "image/jpeg", jpeg.Length, archiveRightsGranted: false);

        Assert.False(outcome.Succeeded);
        Assert.Equal("rights_not_granted", outcome.Rejection!.Code);
        Assert.Empty(store.Puts);
        Assert.Empty(db.EvidenceRecords);
    }

    [Theory]
    [InlineData(CaptureSlot.CareLabel, 1199, false)]   // labels need 1200
    [InlineData(CaptureSlot.CareLabel, 1200, true)]
    [InlineData(CaptureSlot.Zip, 1000, true)]          // zips need 1000
    [InlineData(CaptureSlot.Zip, 999, false)]
    public async Task EnforcesThePerSlotResolutionFloor(CaptureSlot slot, int longEdge, bool shouldPass)
    {
        (CaptureService svc, ToolDbContext db, FakeImageStore store) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream jpeg = Jpeg(longEdge, longEdge / 2);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, slot, EvidenceType.CareLabel, "f", jpeg, "image/jpeg", jpeg.Length, true);

        Assert.Equal(shouldPass, outcome.Succeeded);
        if (!shouldPass)
        {
            Assert.Equal("too_low_resolution", outcome.Rejection!.Code);
            Assert.Empty(store.Puts);   // rejected BEFORE anything is stored
        }
    }

    [Fact]
    public async Task RefusesFormatsWeCannotDecode()
    {
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream jpeg = Jpeg(2000, 1500);

        // HEIC is excluded deliberately — accepting bytes we cannot read would put unusable files
        // in the archive.
        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.CareLabel, EvidenceType.CareLabel, "f", jpeg, "image/heic", jpeg.Length, true);

        Assert.False(outcome.Succeeded);
        Assert.Equal("unsupported_format", outcome.Rejection!.Code);
    }

    [Fact]
    public async Task RefusesBytesThatAreNotAnImage()
    {
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream junk = new(new byte[4096]);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.CareLabel, EvidenceType.CareLabel, "f", junk, "image/jpeg", junk.Length, true);

        Assert.False(outcome.Succeeded);
        Assert.Equal("unreadable", outcome.Rejection!.Code);
    }

    [Fact]
    public async Task RefusesOversizedUploads()
    {
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream jpeg = Jpeg(1400, 1400);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.CareLabel, EvidenceType.CareLabel, "f",
            jpeg, "image/jpeg", CaptureStandard.MaxBytes + 1, true);

        Assert.False(outcome.Succeeded);
        Assert.Equal("too_large", outcome.Rejection!.Code);
    }

    [Fact]
    public async Task AcceptsPngAsWellAsJpeg()
    {
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream png = Png(1400, 1400);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.FlatLayFront, EvidenceType.Other, "f", png, "image/png", png.Length, true);

        Assert.True(outcome.Succeeded);
        Assert.Equal("image/png", outcome.Evidence!.ContentType);
    }

    /// <summary>
    /// The brand label is deliberately NOT required — a large share of real vintage stock has it cut
    /// out, and requiring it would either block honest listings or teach sellers to shoot the wrong
    /// thing. It is requested, not enforced.
    /// </summary>
    [Fact]
    public async Task Completeness_NeedsCareLabelAndFlatLayFront_ButNotTheBrandLabel()
    {
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);

        CaptureCompleteness empty = await svc.GetCompletenessAsync(id);
        Assert.False(empty.IsComplete);
        Assert.Contains(CaptureSlot.CareLabel, empty.MissingRequired);
        Assert.Contains(CaptureSlot.FlatLayFront, empty.MissingRequired);
        Assert.Contains(CaptureSlot.BrandLabel, empty.MissingRequested);

        foreach (CaptureSlot slot in new[] { CaptureSlot.CareLabel, CaptureSlot.FlatLayFront })
        {
            using MemoryStream jpeg = Jpeg(1400, 1400);
            await svc.CaptureAsync(id, slot, EvidenceType.Other, "f", jpeg, "image/jpeg", jpeg.Length, true);
        }

        CaptureCompleteness done = await svc.GetCompletenessAsync(id);
        Assert.True(done.IsComplete);
        Assert.Equal(2, done.CaptureCount);
        // Still requested, and still not blocking.
        Assert.Contains(CaptureSlot.BrandLabel, done.MissingRequested);
    }
// --- Back catalogue: historical uploads (Teodora's v1 reframe) ---

    /// <summary>A JPEG carrying an EXIF DateTimeOriginal, as a real camera photo would.</summary>
    private static MemoryStream JpegTakenOn(int width, int height, DateTime taken)
    {
        using Image<SixLabors.ImageSharp.PixelFormats.Rgba32> img = new(width, height);
        img.Metadata.ExifProfile = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
        img.Metadata.ExifProfile.SetValue(
            SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.DateTimeOriginal,
            taken.ToString("yyyy:MM:dd HH:mm:ss"));
        MemoryStream ms = new();
        img.Save(ms, new JpegEncoder());
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task AHistoricalUploadKeepsTheDateThePhotoWasTaken()
    {
        // The upload date is meaningless for a back-catalogue photo; the capture date is what
        // roughly locates when that garment passed through the shop. Cheap to store, impossible to
        // recover once the file has been re-encoded.
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);
        DateTime taken = new(2026, 3, 14, 9, 41, 0);
        using MemoryStream jpeg = JpegTakenOn(2000, 1500, taken);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.CareLabel, EvidenceType.CareLabel, "care.wash-symbol",
            jpeg, "image/jpeg", jpeg.Length, archiveRightsGranted: true,
            ImageProvenance.HistoricalUpload);

        Assert.True(outcome.Succeeded);
        Assert.Equal(taken, outcome.Evidence!.PhotographedAtLocal);
        // Stored as a wall clock, not an instant — EXIF has no timezone and pretending otherwise
        // invents precision.
        Assert.Equal(DateTimeKind.Unspecified, outcome.Evidence.PhotographedAtLocal!.Value.Kind);
        Assert.NotEqual(outcome.Evidence.PhotographedAtLocal, outcome.Evidence.CreatedAtUtc);
    }

    [Fact]
    public async Task APhotoWithNoExifIsStillStored()
    {
        // Screenshots, re-encodes and anything through a messaging app lose EXIF. A missing date is
        // not a reason to refuse the only surviving photograph of a label.
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream jpeg = Jpeg(2000, 1500);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.CareLabel, EvidenceType.CareLabel, "care.wash-symbol",
            jpeg, "image/jpeg", jpeg.Length, archiveRightsGranted: true,
            ImageProvenance.HistoricalUpload);

        Assert.True(outcome.Succeeded);
        Assert.Null(outcome.Evidence!.PhotographedAtLocal);
    }

    [Fact]
    public async Task AnUndersizedHistoricalPhotoIsKept_BecauseItCannotBeRetaken()
    {
        // The garment sold months ago. This photo is all there will ever be of that label, and a
        // blurry record beats no record. Holding the back catalogue to the capture standard would
        // reject most of the archive this is meant to seed.
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream small = Jpeg(400, 300);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.CareLabel, EvidenceType.CareLabel, "care.wash-symbol",
            small, "image/jpeg", small.Length, archiveRightsGranted: true,
            ImageProvenance.HistoricalUpload);

        Assert.True(outcome.Succeeded);
        Assert.Equal(ImageProvenance.HistoricalUpload, outcome.Evidence!.Provenance);
    }

    [Fact]
    public async Task TheSameUndersizedPhotoIsStillRefusedAsALiveCapture()
    {
        // The standard still bites where it can be met. Someone standing over the garment can move
        // closer and shoot it again; that is the whole difference.
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream small = Jpeg(400, 300);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.CareLabel, EvidenceType.CareLabel, "care.wash-symbol",
            small, "image/jpeg", small.Length, archiveRightsGranted: true,
            ImageProvenance.LiveCapture);

        Assert.False(outcome.Succeeded);
        Assert.Equal("too_low_resolution", outcome.Rejection!.Code);
    }

    [Fact]
    public async Task CaptureDefaultsToLiveCapture_SoProvenanceIsNeverUnknown()
    {
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream jpeg = Jpeg(2000, 1500);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.CareLabel, EvidenceType.CareLabel, "care.wash-symbol",
            jpeg, "image/jpeg", jpeg.Length, archiveRightsGranted: true);

        Assert.Equal(ImageProvenance.LiveCapture, outcome.Evidence!.Provenance);
    }

    [Fact]
    public async Task AHistoricalUploadDoesNotCountTowardsMeetingTheStandard()
    {
        // Otherwise a garment reports itself properly captured on the strength of an old snapshot
        // that was never held to the standard — and the flag stops being able to separate the
        // training-quality set from the rough one, which is its entire purpose.
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);

        foreach (CaptureSlot slot in CaptureStandard.RequiredSlots)
        {
            using MemoryStream jpeg = Jpeg(2000, 1500);
            await svc.CaptureAsync(
                id, slot, EvidenceType.CareLabel, "care.wash-symbol",
                jpeg, "image/jpeg", jpeg.Length, archiveRightsGranted: true,
                ImageProvenance.HistoricalUpload);
        }

        CaptureCompleteness completeness = await svc.GetCompletenessAsync(id);
        Assert.False(completeness.IsComplete);
        Assert.NotEmpty(completeness.MissingRequired);
    }

    [Fact]
    public async Task AZipCaptureCarriesWhetherItIsTheGarmentsOwn()
    {
        (CaptureService svc, ToolDbContext db, _) = Build();
        Guid id = SeedGarment(db);
        using MemoryStream jpeg = Jpeg(2000, 1500);

        CaptureOutcome outcome = await svc.CaptureAsync(
            id, CaptureSlot.Zip, EvidenceType.Zip, "zip.maker-mark",
            jpeg, "image/jpeg", jpeg.Length, archiveRightsGranted: true,
            ImageProvenance.LiveCapture, ZipOriginality.Unsure);

        Assert.True(outcome.Succeeded);
        Assert.Equal(ZipOriginality.Unsure, outcome.Evidence!.ZipOriginality);
    }
}
