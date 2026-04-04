using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class BrandingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BrandingTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_ReturnsDefaults_WhenNoBrandingExists()
    {
        var client = _factory.CreateClient();
        var branding = await client.GetFromJsonAsync<BrandingResponse>("/api/branding", JsonOptions);
        Assert.NotNull(branding);
        Assert.Equal("#FAF9F7", branding.BgPrimary);
        Assert.Equal("#8F1D31", branding.Accent);
        Assert.Equal("Playfair Display", branding.FontDisplay);
        Assert.Equal("Work Sans", branding.FontBody);
        Assert.Null(branding.LogoUrl);
    }

    [Fact]
    public async Task Update_AsAdmin_SavesAndReturnsBranding()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "branding-update@test.com");

        var response = await client.PutAsJsonAsync("/api/branding", new
        {
            logoUrl = "https://example.com/logo.png",
            bgPrimary = "#FFFFFF",
            bgSecondary = "#F0F0F0",
            bgCard = "#FAFAFA",
            bgDark = "#111111",
            textPrimary = "#000000",
            textSecondary = "#333333",
            textMuted = "#666666",
            textInverse = "#FFFFFF",
            accent = "#FF0000",
            accentHover = "#CC0000",
            fontDisplay = "Georgia",
            fontBody = "Arial"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var branding = await response.Content.ReadFromJsonAsync<BrandingResponse>(JsonOptions);
        Assert.NotNull(branding);
        Assert.Equal("https://example.com/logo.png", branding.LogoUrl);
        Assert.Equal("#FFFFFF", branding.BgPrimary);
        Assert.Equal("#FF0000", branding.Accent);
        Assert.Equal("Georgia", branding.FontDisplay);
    }

    [Fact]
    public async Task Update_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/branding", new
        {
            bgPrimary = "#000000",
            bgSecondary = "#000000",
            bgCard = "#000000",
            bgDark = "#000000",
            textPrimary = "#FFFFFF",
            textSecondary = "#FFFFFF",
            textMuted = "#FFFFFF",
            textInverse = "#000000",
            accent = "#FF0000",
            accentHover = "#CC0000",
            fontDisplay = "Arial",
            fontBody = "Arial"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "branding-customer@test.com");

        var response = await client.PutAsJsonAsync("/api/branding", new
        {
            bgPrimary = "#000000",
            bgSecondary = "#000000",
            bgCard = "#000000",
            bgDark = "#000000",
            textPrimary = "#FFFFFF",
            textSecondary = "#FFFFFF",
            textMuted = "#FFFFFF",
            textInverse = "#000000",
            accent = "#FF0000",
            accentHover = "#CC0000",
            fontDisplay = "Arial",
            fontBody = "Arial"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private record BrandingResponse(string? LogoUrl, string BgPrimary, string BgSecondary, string BgCard, string BgDark, string TextPrimary, string TextSecondary, string TextMuted, string TextInverse, string Accent, string AccentHover, string FontDisplay, string FontBody);
}
