using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services.CrossListing;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// The crosslister's job is translating one internal record into each platform's fields correctly
/// (brief §6). These cover the translation and — more importantly — the refusals: the cases where
/// posting would be wrong, and the Vinted rules that exist to keep sellers' accounts safe.
/// </summary>
public class CrossListingTests
{
    private static CanonicalListing Listing(
        string era = "1970s",
        decimal price = 60m,
        string title = "Handmade Folk Floral Maxi Dress",
        string? description = "A navy floral prairie maxi with a lace-trimmed bib.",
        int images = 2,
        IReadOnlyDictionary<string, decimal>? measurements = null,
        bool measurementsConfirmed = false) => new()
        {
            SourceId = Guid.NewGuid(),
            Sku = "ER-00042",
            Title = title,
            Description = description ?? string.Empty,
            Price = price,
            Era = era,
            Category = "70s",
            Size = "10",
            Condition = "very good",
            ImageUrls = [.. Enumerable.Range(0, images).Select(i => $"https://img.test/{i}.webp")],
            MeasurementsCm = measurements ?? new Dictionary<string, decimal>(),
            MeasurementsConfirmed = measurementsConfirmed,
        };

    // ---- Transport: hybrid by design (brief §3) ----

    [Fact]
    public void ApiPlatformsRunServerSide_ExtensionPlatformsDoNot()
    {
        Assert.Equal(ListingTransport.ServerApi, new EtsyAdapter().Transport);
        Assert.Equal(ListingTransport.ServerApi, new EbayAdapter().Transport);
        Assert.Equal(ListingTransport.SellerBrowserExtension, new VintedAdapter().Transport);
        Assert.Equal(ListingTransport.SellerBrowserExtension, new DepopAdapter().Transport);
    }

    [Fact]
    public void ExtensionPlatformsAlwaysCarryPasteableContent()
    {
        // Brief §3: an extension failure must never leave the seller believing something went live.
        // That is only possible if the content exists whether or not the extension worked.
        foreach (IMarketplaceAdapter adapter in new IMarketplaceAdapter[] { new VintedAdapter(), new DepopAdapter() })
        {
            PasteFallback? fallback = adapter.BuildFallback(Listing());
            Assert.NotNull(fallback);
            Assert.False(string.IsNullOrWhiteSpace(fallback.Title));
            Assert.False(string.IsNullOrWhiteSpace(fallback.Description));
            Assert.Equal(60m, fallback.Price);
        }
    }

    [Fact]
    public void ExtensionPlatformsWarnThatTheSellersBrowserIsRequired()
    {
        ListingValidation v = new VintedAdapter().Validate(Listing());
        Assert.Contains(v.Warnings, w => w.Field == "Transport" && w.Problem.Contains("browser"));
    }

    // ---- Unresearched field mappings must refuse, not guess (brief §6) ----

    [Fact]
    public void PlatformsWithoutResearchedFieldMappings_RefuseToPublish()
    {
        foreach (IMarketplaceAdapter adapter in new IMarketplaceAdapter[] { new EbayAdapter(), new VintedAdapter(), new DepopAdapter() })
        {
            Assert.Equal(FieldMappingResearch.Unresearched, adapter.Research);
            ListingValidation v = adapter.Validate(Listing());
            Assert.False(v.CanPublish, $"{adapter.Platform} should refuse until its fields are mapped");
            Assert.Contains(v.Blocking, b => b.Field == "Platform");
        }
    }

    [Fact]
    public void Etsy_IsResearched_AndPublishesAGoodListing()
    {
        EtsyAdapter etsy = new();
        Assert.Equal(FieldMappingResearch.Documented, etsy.Research);

        ListingValidation v = etsy.Validate(Listing());
        Assert.True(v.CanPublish);

        IReadOnlyDictionary<string, string> fields = etsy.MapFields(Listing());
        Assert.Equal("1970_1979", fields["when_made"]);
        Assert.Equal("60.00", fields["price"]);
        Assert.Equal("1", fields["quantity"]);
    }

    // ---- The era gate: the same defect that put 1950s pieces on Etsy as made in the 2000s ----

    [Theory]
    [InlineData("1950s")]
    [InlineData("1960s")]
    [InlineData("circa 1972")]
    public void AnEraThatResolvesToADecade_Passes(string era)
    {
        Assert.True(new EtsyAdapter().Validate(Listing(era: era)).CanPublish);
    }

    [Theory]
    [InlineData("vintage")]
    [InlineData("mid-century")]
    [InlineData("")]
    public void AnEraThatResolvesToNothing_BlocksEveryPlatform(string era)
    {
        foreach (IMarketplaceAdapter adapter in new IMarketplaceAdapter[] { new EtsyAdapter(), new VintedAdapter(), new DepopAdapter() })
        {
            ListingValidation v = adapter.Validate(Listing(era: era));
            Assert.False(v.CanPublish);
            Assert.Contains(v.Blocking, b => b.Field == "Era");
        }
    }

    // ---- Basics that block anywhere ----

    [Fact]
    public void NoPhotographs_Blocks()
    {
        ListingValidation v = new EtsyAdapter().Validate(Listing(images: 0));
        Assert.False(v.CanPublish);
        Assert.Contains(v.Blocking, b => b.Field == "Images");
    }

    [Fact]
    public void ZeroPrice_Blocks()
    {
        ListingValidation v = new EtsyAdapter().Validate(Listing(price: 0m));
        Assert.False(v.CanPublish);
        Assert.Contains(v.Blocking, b => b.Field == "Price");
    }

    [Fact]
    public void MissingDescription_Blocks()
    {
        ListingValidation v = new EtsyAdapter().Validate(Listing(description: null));
        Assert.False(v.CanPublish);
        Assert.Contains(v.Blocking, b => b.Field == "Description");
    }

    // ---- Measurements: a machine proposal must not reach a live listing (brief §5) ----

    [Fact]
    public void UnconfirmedMeasurements_AreFlagged()
    {
        CanonicalListing l = Listing(
            measurements: new Dictionary<string, decimal> { ["Pit to pit"] = 47m },
            measurementsConfirmed: false);

        ListingValidation v = new EtsyAdapter().Validate(l);
        Assert.Contains(v.Warnings, w => w.Field == "Measurements");
    }

    [Fact]
    public void ConfirmedMeasurements_AreNotFlagged_AndReachTheDescription()
    {
        CanonicalListing l = Listing(
            measurements: new Dictionary<string, decimal> { ["Pit to pit"] = 47m, ["Length"] = 132m },
            measurementsConfirmed: true);

        Assert.DoesNotContain(new EtsyAdapter().Validate(l).Warnings, w => w.Field == "Measurements");
        Assert.Contains("Pit to pit 47cm", new VintedAdapter().BuildFallback(l).Description);
    }

    // ---- Title limits differ wildly per platform (brief §6) ----

    [Fact]
    public void TitlesAreTruncatedToEachPlatformsLimit_NotOurs()
    {
        string longTitle = new string('x', 300);
        CanonicalListing l = Listing(title: longTitle);

        Assert.True(new EtsyAdapter().MapFields(l)["title"].Length <= 140);
        Assert.True(new VintedAdapter().MapFields(l)["title"].Length <= 100);
        Assert.True(new DepopAdapter().MapFields(l)["title"].Length <= 65);
    }

    [Fact]
    public void AnOverlongTitleWarnsRatherThanBlocking()
    {
        ListingValidation v = new EtsyAdapter().Validate(Listing(title: new string('x', 300)));
        Assert.True(v.CanPublish);
        Assert.Contains(v.Warnings, w => w.Field == "Title");
    }

    // ---- The canonical record ----

    [Fact]
    public void APieceOnSale_ListsAtWhatTheBuyerPays()
    {
        Product product = new()
        {
            Name = "Reduced Dress", Sku = "ER-1", Slug = "reduced", Description = "d",
            Price = 100m, SalePrice = 70m, Era = "1970s", Category = "70s", Size = "10",
            Condition = "good", ImageUrl = "https://img.test/a.webp",
        };

        Assert.Equal(70m, CanonicalListing.FromProduct(product).Price);
    }

    [Fact]
    public void ThePrimaryImageLeads()
    {
        Product product = new()
        {
            Name = "D", Sku = "ER-2", Slug = "d", Description = "d", Price = 10m,
            Era = "1970s", Category = "70s", Size = "10", Condition = "good",
            ImageUrl = "https://img.test/primary.webp",
            AdditionalImageUrls = ["https://img.test/second.webp"],
        };

        CanonicalListing l = CanonicalListing.FromProduct(product);
        Assert.Equal("https://img.test/primary.webp", l.ImageUrls[0]);
        Assert.Equal(2, l.ImageUrls.Count);
    }

    // ---- Depop hashtags are mechanical, from the garment (the brief rules out AI-written listings) ----

    [Fact]
    public void DepopDescriptionCarriesDecadeHashtags()
    {
        string description = new DepopAdapter().BuildFallback(Listing()).Description;
        Assert.Contains("#vintage", description);
        Assert.Contains("#1970s", description);
    }

    [Fact]
    public void VintedDescriptionPointsBackAtTheShop()
    {
        Assert.Contains("edenrelics.co.uk", new VintedAdapter().BuildFallback(Listing()).Description);
    }
}
