using Eden_Relics_BE.Services;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Era is a free-text admin field and <c>when_made</c> is a public claim about a garment's age,
/// so the mapping must never guess. It used to fall back to "2000_2009" for anything it didn't
/// recognise, which silently declared 1950s and 1960s pieces as made in the 2000s.
/// </summary>
public class EtsyWhenMadeTests
{
    [Theory]
    [InlineData("1950s", "1950_1959")]
    [InlineData("1960s", "1960_1969")]
    [InlineData("1970s", "1970_1979")]
    [InlineData("1980s", "1980_1989")]
    [InlineData("1990s", "1990_1999")]
    [InlineData("2000s", "2000_2009")]
    [InlineData("2010s", "2010_2019")]
    public void MapsEveryDecadeTheCatalogueUses(string era, string expected)
    {
        Assert.True(MarketplaceService.TryMapEraToEtsyWhenMade(era, out string whenMade));
        Assert.Equal(expected, whenMade);
    }

    [Fact]
    public void MapsThe2020sToEtsysTruncatedBucket()
    {
        Assert.True(MarketplaceService.TryMapEraToEtsyWhenMade("2020s", out string whenMade));
        Assert.Equal("2020_2025", whenMade);
    }

    [Theory]
    [InlineData("circa 1972", "1970_1979")]
    [InlineData("Late 1960s", "1960_1969")]
    [InlineData("1978", "1970_1979")]
    public void ReadsADecadeOutOfLooserFreeText(string era, string expected)
    {
        Assert.True(MarketplaceService.TryMapEraToEtsyWhenMade(era, out string whenMade));
        Assert.Equal(expected, whenMade);
    }

    [Theory]
    [InlineData("70s", "1970_1979")]
    [InlineData("'60s", "1960_1969")]
    [InlineData("90s", "1990_1999")]
    [InlineData("y2k", "2000_2009")]
    public void AcceptsTheSitesOwnShortDecadeTokens(string era, string expected)
    {
        Assert.True(MarketplaceService.TryMapEraToEtsyWhenMade(era, out string whenMade));
        Assert.Equal(expected, whenMade);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("vintage")]
    [InlineData("mid-century")]
    [InlineData("1890s")]   // Etsy has no decade token below 1900
    [InlineData("2030s")]   // beyond Etsy's newest bucket
    public void RefusesRatherThanGuessing(string? era)
    {
        Assert.False(MarketplaceService.TryMapEraToEtsyWhenMade(era, out string whenMade));
        Assert.Equal(string.Empty, whenMade);
    }

    [Fact]
    public void NeverSilentlyFallsBackToThe2000s()
    {
        // The specific regression: an unrecognised era must NOT come back as a 2000s listing.
        Assert.False(MarketplaceService.TryMapEraToEtsyWhenMade("no idea", out string whenMade));
        Assert.NotEqual("2000_2009", whenMade);
    }
}
