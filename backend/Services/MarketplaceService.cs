using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class MarketplaceService(
    IRepository<ProductListing> listings,
    IRepository<Product> products,
    IRepository<EtsyToken> etsyTokens,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<MarketplaceService> logger) : IMarketplaceService
{
    // --- Listing management ---

    public async Task<List<ProductListingDto>> GetListingsAsync(Guid productId)
    {
        List<ProductListing> rows = await listings.Query()
            .Where(l => l.ProductId == productId)
            .ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<ProductListingDto?> AddListingAsync(CreateListingDto dto)
    {
        Product? product = await products.GetByIdAsync(dto.ProductId);
        if (product is null)
        {
            return null;
        }

        ProductListing listing = new()
        {
            ProductId = dto.ProductId,
            Platform = dto.Platform,
            ExternalListingId = dto.ExternalListingId,
            ExternalUrl = dto.ExternalUrl,
            Status = "Active"
        };
        await listings.AddAsync(listing);
        return ToDto(listing);
    }

    public async Task<ProductListingDto?> UpdateListingStatusAsync(Guid id, UpdateListingStatusDto dto)
    {
        ProductListing? listing = await listings.Query()
            .Include(l => l.Product)
            .ThenInclude(p => p.Listings)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null)
        {
            return null;
        }

        listing.Status = dto.Status;

        if (dto.Status == "Sold")
        {
            listing.Product.Status = ProductStatus.Sold;
            foreach (ProductListing other in listing.Product.Listings.Where(l => l.Id != id && l.Status == "Active"))
            {
                other.Status = "PendingRemoval";
            }
        }

        await listings.UpdateAsync(listing);
        return ToDto(listing);
    }

    public async Task<bool> RemoveListingAsync(Guid id)
    {
        ProductListing? listing = await listings.GetByIdAsync(id);
        if (listing is null)
        {
            return false;
        }
        listing.Status = "Removed";
        await listings.UpdateAsync(listing);
        return true;
    }

    public async Task<bool> MarkSoldAsync(Guid productId, MarkSoldDto dto)
    {
        Product? product = await products.Query()
            .Include(p => p.Listings)
            .FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null)
        {
            return false;
        }

        product.Status = ProductStatus.Sold;

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

        await products.UpdateAsync(product);
        return true;
    }

    public async Task<List<PendingRemovalDto>> GetPendingRemovalsAsync()
    {
        List<ProductListing> pending = await listings.Query()
            .Include(l => l.Product)
            .Where(l => l.Status == "PendingRemoval")
            .ToListAsync();

        return pending.Select(l => new PendingRemovalDto(
            l.Id, l.ProductId, l.Product.Name, l.Platform, l.ExternalUrl
        )).ToList();
    }

    public async Task<bool> AcknowledgeRemovalAsync(Guid id)
    {
        ProductListing? listing = await listings.GetByIdAsync(id);
        if (listing is null)
        {
            return false;
        }
        listing.Status = "Removed";
        await listings.UpdateAsync(listing);
        return true;
    }

    // --- Etsy OAuth ---

    public EtsyConnectDto? BuildEtsyConnect()
    {
        string? apiKey = configuration["Etsy:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
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

        return new EtsyConnectDto(url, state, codeVerifier);
    }

    public async Task<EtsyCallbackResult> CompleteEtsyCallbackAsync(EtsyCallbackDto dto)
    {
        string? apiKey = configuration["Etsy:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new EtsyCallbackResult(false, "Etsy API key not configured.", null);
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
            return new EtsyCallbackResult(false, "Failed to exchange authorization code.", null);
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        string accessToken = root.GetProperty("access_token").GetString()!;
        string refreshToken = root.GetProperty("refresh_token").GetString()!;
        int expiresIn = root.GetProperty("expires_in").GetInt32();

        // Fetch shop ID
        string shopId = await FetchShopId(client, apiKey, accessToken) ?? "";

        // Store token (replace any existing — tokens are IHardDeletable, so physically removed)
        List<EtsyToken> existing = await etsyTokens.Query().ToListAsync();
        await etsyTokens.RemoveRangeAsync(existing);

        await etsyTokens.AddAsync(new EtsyToken
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ShopId = shopId,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn),
        });

        return new EtsyCallbackResult(true, "Etsy connected.", shopId);
    }

    public async Task DisconnectEtsyAsync()
    {
        List<EtsyToken> tokens = await etsyTokens.Query().ToListAsync();
        await etsyTokens.RemoveRangeAsync(tokens);
    }

    public async Task<EtsyStatusDto> GetEtsyStatusAsync()
    {
        EtsyToken? token = await etsyTokens.Query().FirstOrDefaultAsync();
        bool connected = token is not null && !string.IsNullOrEmpty(token.ShopId);
        bool apiKeyConfigured = !string.IsNullOrWhiteSpace(configuration["Etsy:ApiKey"]);

        return new EtsyStatusDto(apiKeyConfigured, connected, token?.ShopId);
    }

    // --- Etsy listing operations ---

    public async Task<CreateEtsyListingResult> CreateEtsyListingAsync(EtsyListingRequest dto)
    {
        string? apiKey = configuration["Etsy:ApiKey"];
        string? accessToken = await EnsureValidEtsyTokenAsync();

        EtsyToken? token = await etsyTokens.Query().FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(apiKey) || accessToken is null || token is null || string.IsNullOrEmpty(token.ShopId))
        {
            return new CreateEtsyListingResult(CreateEtsyListingOutcome.NotConnected, null,
                "Etsy is not connected. Please connect via the Etsy OAuth flow first.");
        }

        Product? product = await products.GetByIdAsync(dto.ProductId);
        if (product is null)
        {
            return new CreateEtsyListingResult(CreateEtsyListingOutcome.ProductNotFound, null, "Product not found.");
        }

        // Refuse rather than mis-declare the decade: Etsy's when_made is a public claim about
        // the garment's age, and the vintage category depends on it.
        if (!TryMapEraToEtsyWhenMade(product.Era, out string whenMade))
        {
            return new CreateEtsyListingResult(CreateEtsyListingOutcome.UnmappableEra, null,
                $"Era '{product.Era}' doesn't map to an Etsy decade. Set the product's era to a "
                + "decade Etsy recognises (e.g. '1960s') before listing.");
        }

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
            ["when_made"] = whenMade,
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
            return new CreateEtsyListingResult(CreateEtsyListingOutcome.EtsyError, null, $"Etsy API error: {error}");
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
        await listings.AddAsync(listing);

        return new CreateEtsyListingResult(CreateEtsyListingOutcome.Success, ToDto(listing), null);
    }

    // --- Listing text generator ---

    public async Task<GeneratedListingDto?> GenerateListingTextAsync(Guid productId, string platform)
    {
        Product? product = await products.GetByIdAsync(productId);
        if (product is null)
        {
            return null;
        }

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

        return new GeneratedListingDto(title, description, product.Price, product.ImageUrl);
    }

    // --- Etsy token management ---

    private async Task<string?> EnsureValidEtsyTokenAsync()
    {
        EtsyToken? token = await etsyTokens.Query().FirstOrDefaultAsync();
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
        await etsyTokens.UpdateAsync(token);

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

    /// <summary>
    /// Maps a product's era to Etsy's <c>when_made</c> decade token, or returns false when the
    /// era can't be read as a decade.
    ///
    /// Era is a free-text admin field, so this must not guess. It used to fall back to
    /// "2000_2009" for anything it didn't recognise — which silently declared every 1950s and
    /// 1960s piece (both are live catalogue categories) as made 2000–2009, along with any
    /// typo like "60s" or "Late 1970s". That is wrong on a live listing and puts the piece
    /// outside Etsy's 20-years-plus vintage rule.
    /// </summary>
    public static bool TryMapEraToEtsyWhenMade(string? era, out string whenMade)
    {
        whenMade = string.Empty;
        if (string.IsNullOrWhiteSpace(era))
        {
            return false;
        }

        // A full year anywhere in the text ("1970s", "circa 1972", "1960s/70s" → first wins).
        // Trailing (?!\d) rather than \b: "1950s" has no word boundary between "0" and "s",
        // so \b would reject the single most common way an era is written here.
        Match year = Regex.Match(era, @"\b(1[89]\d{2}|20[0-2]\d)(?!\d)");
        int decade;
        if (year.Success)
        {
            decade = int.Parse(year.Groups[1].Value) / 10 * 10;
        }
        else
        {
            // The site's own short decade tokens, which an admin may type instead ('70s, y2k).
            Match shortForm = Regex.Match(era.Trim(), @"^'?(\d{2})s$");
            if (shortForm.Success)
            {
                int twoDigit = int.Parse(shortForm.Groups[1].Value);
                // 30s–90s read as last century; 00s–20s as this one. Nothing here is older.
                decade = twoDigit >= 30 ? 1900 + twoDigit : 2000 + twoDigit;
            }
            else if (era.Trim().Equals("y2k", StringComparison.OrdinalIgnoreCase))
            {
                decade = 2000;
            }
            else
            {
                return false;
            }
        }

        // Etsy has no decade token below 1900, and its newest bucket stops at 2025.
        if (decade < 1900 || decade > 2020)
        {
            return false;
        }

        whenMade = decade == 2020 ? "2020_2025" : $"{decade}_{decade + 9}";
        return true;
    }

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
