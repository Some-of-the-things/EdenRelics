using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class MarketplaceController(EdenRelicsDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<MarketplaceController> logger) : ControllerBase
{
    // --- Listing management ---

    [HttpGet("listings/{productId:guid}")]
    public async Task<ActionResult<List<ProductListingDto>>> GetListings(Guid productId)
    {
        List<ProductListing> listings = await context.ProductListings
            .Where(l => l.ProductId == productId)
            .ToListAsync();
        return Ok(listings.Select(ToDto).ToList());
    }

    [HttpPost("listings")]
    public async Task<ActionResult<ProductListingDto>> AddListing([FromBody] CreateListingDto dto)
    {
        Product? product = await context.Products.FindAsync(dto.ProductId);
        if (product is null) { return NotFound(new { message = "Product not found." }); }

        ProductListing listing = new()
        {
            ProductId = dto.ProductId,
            Platform = dto.Platform,
            ExternalListingId = dto.ExternalListingId,
            ExternalUrl = dto.ExternalUrl,
            Status = "Active"
        };
        context.ProductListings.Add(listing);
        await context.SaveChangesAsync();
        return Ok(ToDto(listing));
    }

    [HttpPut("listings/{id:guid}/status")]
    public async Task<ActionResult<ProductListingDto>> UpdateListingStatus(Guid id, [FromBody] UpdateListingStatusDto dto)
    {
        ProductListing? listing = await context.ProductListings
            .Include(l => l.Product)
            .ThenInclude(p => p.Listings)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) { return NotFound(); }

        listing.Status = dto.Status;

        if (dto.Status == "Sold")
        {
            listing.Product.InStock = false;
            foreach (ProductListing other in listing.Product.Listings.Where(l => l.Id != id && l.Status == "Active"))
            {
                other.Status = "PendingRemoval";
            }
        }

        await context.SaveChangesAsync();
        return Ok(ToDto(listing));
    }

    [HttpDelete("listings/{id:guid}")]
    public async Task<IActionResult> RemoveListing(Guid id)
    {
        ProductListing? listing = await context.ProductListings.FindAsync(id);
        if (listing is null) { return NotFound(); }
        listing.Status = "Removed";
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("mark-sold/{productId:guid}")]
    public async Task<ActionResult> MarkSold(Guid productId, [FromBody] MarkSoldDto dto)
    {
        Product? product = await context.Products
            .Include(p => p.Listings)
            .FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null) { return NotFound(); }

        product.InStock = false;

        foreach (ProductListing listing in product.Listings.Where(l => l.Status == "Active"))
        {
            if (listing.Platform == dto.SoldOn)
            {
                listing.Status = "Sold";
            }
            else
            {
                listing.Status = "PendingRemoval";
                if (listing.Platform == "Etsy" && listing.ExternalListingId is not null)
                {
                    await TryDeactivateEtsyListing(listing.ExternalListingId);
                }
            }
        }

        await context.SaveChangesAsync();
        return Ok(new { message = $"Product marked as sold on {dto.SoldOn}. Other listings flagged for removal." });
    }

    [HttpGet("pending-removals")]
    public async Task<ActionResult<List<PendingRemovalDto>>> GetPendingRemovals()
    {
        List<ProductListing> pending = await context.ProductListings
            .Include(l => l.Product)
            .Where(l => l.Status == "PendingRemoval")
            .ToListAsync();

        return Ok(pending.Select(l => new PendingRemovalDto(
            l.Id, l.ProductId, l.Product.Name, l.Platform, l.ExternalUrl
        )).ToList());
    }

    [HttpPost("acknowledge-removal/{id:guid}")]
    public async Task<IActionResult> AcknowledgeRemoval(Guid id)
    {
        ProductListing? listing = await context.ProductListings.FindAsync(id);
        if (listing is null) { return NotFound(); }
        listing.Status = "Removed";
        await context.SaveChangesAsync();
        return Ok();
    }

    // --- Etsy OAuth ---

    [HttpGet("etsy/connect")]
    public ActionResult EtsyConnect()
    {
        string? apiKey = configuration["Etsy:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BadRequest(new { message = "Etsy API key not configured. Set Etsy:ApiKey." });
        }

        string redirectUri = configuration["Etsy:RedirectUri"] ?? "http://localhost:4200/admin?etsy=callback";

        // Generate PKCE code verifier and challenge
        byte[] verifierBytes = new byte[96];
        RandomNumberGenerator.Fill(verifierBytes);
        string codeVerifier = Convert.ToBase64String(verifierBytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=')[..128];

        byte[] challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        string codeChallenge = Convert.ToBase64String(challengeBytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        string state = Guid.NewGuid().ToString("N");
        string scopes = "listings_r listings_w listings_d shops_r";

        string url = $"https://www.etsy.com/oauth/connect" +
            $"?response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString(scopes)}" +
            $"&client_id={Uri.EscapeDataString(apiKey)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            $"&code_challenge_method=S256";

        return Ok(new { url, state, codeVerifier });
    }

    [HttpPost("etsy/callback")]
    public async Task<ActionResult> EtsyCallback([FromBody] EtsyCallbackDto dto)
    {
        string? apiKey = configuration["Etsy:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BadRequest(new { message = "Etsy API key not configured." });
        }

        string redirectUri = configuration["Etsy:RedirectUri"] ?? "http://localhost:4200/admin?etsy=callback";

        HttpClient client = httpClientFactory.CreateClient();
        FormUrlEncodedContent content = new([
            new("grant_type", "authorization_code"),
            new("client_id", apiKey),
            new("redirect_uri", redirectUri),
            new("code", dto.Code),
            new("code_verifier", dto.CodeVerifier),
        ]);

        HttpResponseMessage response = await client.PostAsync("https://api.etsy.com/v3/public/oauth/token", content);
        string body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Etsy token exchange failed: {Status}", response.StatusCode);
            return BadRequest(new { message = "Failed to exchange authorization code." });
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        string accessToken = root.GetProperty("access_token").GetString()!;
        string refreshToken = root.GetProperty("refresh_token").GetString()!;
        int expiresIn = root.GetProperty("expires_in").GetInt32();

        // Fetch shop ID
        string shopId = await FetchShopId(client, apiKey, accessToken) ?? "";

        // Store token (replace any existing)
        List<EtsyToken> existing = await context.EtsyTokens.ToListAsync();
        context.EtsyTokens.RemoveRange(existing);

        context.EtsyTokens.Add(new EtsyToken
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ShopId = shopId,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn),
        });
        await context.SaveChangesAsync();

        return Ok(new { message = "Etsy connected.", shopId });
    }

    [HttpPost("etsy/disconnect")]
    public async Task<ActionResult> EtsyDisconnect()
    {
        List<EtsyToken> tokens = await context.EtsyTokens.ToListAsync();
        context.EtsyTokens.RemoveRange(tokens);
        await context.SaveChangesAsync();
        return Ok(new { message = "Etsy disconnected." });
    }

    [HttpGet("etsy/status")]
    public async Task<ActionResult<object>> EtsyStatus()
    {
        EtsyToken? token = await context.EtsyTokens.FirstOrDefaultAsync();
        bool connected = token is not null && !string.IsNullOrEmpty(token.ShopId);
        bool apiKeyConfigured = !string.IsNullOrWhiteSpace(configuration["Etsy:ApiKey"]);

        return Ok(new
        {
            apiKeyConfigured,
            connected,
            shopId = token?.ShopId
        });
    }

    // --- Etsy listing operations ---

    [HttpPost("etsy/create-listing")]
    public async Task<ActionResult<ProductListingDto>> CreateEtsyListing([FromBody] EtsyListingRequest dto)
    {
        string? apiKey = configuration["Etsy:ApiKey"];
        string? accessToken = await EnsureValidEtsyTokenAsync();

        EtsyToken? token = await context.EtsyTokens.FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(apiKey) || accessToken is null || token is null || string.IsNullOrEmpty(token.ShopId))
        {
            return BadRequest(new { message = "Etsy is not connected. Please connect via the Etsy OAuth flow first." });
        }

        Product? product = await context.Products.FindAsync(dto.ProductId);
        if (product is null) { return NotFound(new { message = "Product not found." }); }

        HttpClient client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        Dictionary<string, string> listingData = new()
        {
            ["quantity"] = "1",
            ["title"] = dto.Title ?? product.Name,
            ["description"] = dto.Description ?? product.Description,
            ["price"] = product.Price.ToString("F2"),
            ["who_made"] = "someone_else",
            ["when_made"] = MapEraToEtsyWhenMade(product.Era),
            ["taxonomy_id"] = "1759",
            ["is_supply"] = "false",
            ["type"] = "physical",
        };

        FormUrlEncodedContent content = new(listingData);
        HttpResponseMessage response = await client.PostAsync(
            $"https://openapi.etsy.com/v3/application/shops/{token.ShopId}/listings", content);

        string responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            string error = TryExtractError(responseJson);
            return BadRequest(new { message = $"Etsy API error: {error}" });
        }

        using JsonDocument listingDoc = JsonDocument.Parse(responseJson);
        string listingId = listingDoc.RootElement.GetProperty("listing_id").GetInt64().ToString();

        ProductListing listing = new()
        {
            ProductId = product.Id,
            Platform = "Etsy",
            ExternalListingId = listingId,
            ExternalUrl = $"https://www.etsy.com/listing/{listingId}",
            Status = "Active"
        };
        context.ProductListings.Add(listing);
        await context.SaveChangesAsync();

        return Ok(ToDto(listing));
    }

    // --- Listing text generator ---

    [HttpGet("generate-listing/{productId:guid}")]
    public async Task<ActionResult<GeneratedListingDto>> GenerateListingText(Guid productId, [FromQuery] string platform)
    {
        Product? product = await context.Products.FindAsync(productId);
        if (product is null) { return NotFound(); }

        string title;
        string description;
        string hashtags = $"#vintage #vintagefashion #vintagedress #{product.Category} #{product.Era.Replace("s", "")}s #sustainablefashion";

        if (platform.Equals("Depop", StringComparison.OrdinalIgnoreCase))
        {
            title = $"{product.Name} — {product.Era} Vintage";
            description = $"{product.Description}\n\nSize: UK {product.Size}\nEra: {product.Era}\nCondition: {product.Condition}\n\n{hashtags}";
        }
        else if (platform.Equals("Vinted", StringComparison.OrdinalIgnoreCase))
        {
            title = $"{product.Name} {product.Era} Vintage Size {product.Size}";
            description = $"{product.Description}\n\nSize: UK {product.Size}\nEra: {product.Era}\nCondition: {product.Condition}\n\nAlso available at edenrelics.co.uk";
        }
        else
        {
            title = product.Name;
            description = product.Description;
        }

        return Ok(new GeneratedListingDto(title, description, product.Price, product.ImageUrl));
    }

    // --- Etsy token management ---

    private async Task<string?> EnsureValidEtsyTokenAsync()
    {
        EtsyToken? token = await context.EtsyTokens.FirstOrDefaultAsync();
        if (token is null) { return null; }

        if (token.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(5))
        {
            return token.AccessToken;
        }

        string? apiKey = configuration["Etsy:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) { return null; }

        HttpClient client = httpClientFactory.CreateClient();
        FormUrlEncodedContent content = new([
            new("grant_type", "refresh_token"),
            new("client_id", apiKey),
            new("refresh_token", token.RefreshToken),
        ]);

        HttpResponseMessage response = await client.PostAsync("https://api.etsy.com/v3/public/oauth/token", content);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Etsy token refresh failed: {Status}", response.StatusCode);
            return null;
        }

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        token.AccessToken = root.GetProperty("access_token").GetString()!;
        token.RefreshToken = root.GetProperty("refresh_token").GetString()!;
        token.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32());
        await context.SaveChangesAsync();

        logger.LogInformation("Etsy token refreshed, expires at {ExpiresAt}", token.ExpiresAtUtc);
        return token.AccessToken;
    }

    private async Task<string?> FetchShopId(HttpClient client, string apiKey, string accessToken)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            HttpResponseMessage response = await client.GetAsync("https://openapi.etsy.com/v3/application/users/me/shops");
            if (!response.IsSuccessStatusCode) { return null; }

            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("results", out JsonElement results) && results.GetArrayLength() > 0)
            {
                return results[0].GetProperty("shop_id").GetInt64().ToString();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Etsy shop ID");
        }
        return null;
    }

    private async Task TryDeactivateEtsyListing(string listingId)
    {
        string? apiKey = configuration["Etsy:ApiKey"];
        string? accessToken = await EnsureValidEtsyTokenAsync();
        if (string.IsNullOrWhiteSpace(apiKey) || accessToken is null) { return; }

        try
        {
            HttpClient client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            FormUrlEncodedContent content = new(new Dictionary<string, string>
            {
                ["state"] = "inactive"
            });
            await client.PutAsync($"https://openapi.etsy.com/v3/application/listings/{listingId}", content);
        }
        catch { /* Best effort */ }
    }

    private static string MapEraToEtsyWhenMade(string era) => era switch
    {
        "1970s" => "1970_1979",
        "1980s" => "1980_1989",
        "1990s" => "1990_1999",
        "2000s" => "2000_2009",
        "2020s" => "2020_2025",
        _ => "2000_2009"
    };

    private static string TryExtractError(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out JsonElement err))
            {
                return err.GetString() ?? json;
            }
        }
        catch { }
        return json;
    }

    private static ProductListingDto ToDto(ProductListing l) => new(
        l.Id, l.ProductId, l.Platform, l.ExternalListingId, l.ExternalUrl, l.Status
    );
}

public record ProductListingDto(Guid Id, Guid ProductId, string Platform, string? ExternalListingId, string? ExternalUrl, string Status);
public record CreateListingDto(Guid ProductId, string Platform, string? ExternalListingId, string? ExternalUrl);
public record UpdateListingStatusDto(string Status);
public record MarkSoldDto(string SoldOn);
public record PendingRemovalDto(Guid ListingId, Guid ProductId, string ProductName, string Platform, string? ExternalUrl);
public record EtsyListingRequest(Guid ProductId, string? Title, string? Description);
public record EtsyCallbackDto(string Code, string CodeVerifier);
public record GeneratedListingDto(string Title, string Description, decimal Price, string ImageUrl);
