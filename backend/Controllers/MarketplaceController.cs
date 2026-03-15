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
public class MarketplaceController(EdenRelicsDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration) : ControllerBase
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

        // If marked as sold, flag all other active listings for removal and mark product as out of stock
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

    // --- Mark sold from any channel (called when Stripe webhook fires or manually) ---

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
                // If it's an Etsy listing, try to deactivate it automatically
                if (listing.Platform == "Etsy" && listing.ExternalListingId is not null)
                {
                    await TryDeactivateEtsyListing(listing.ExternalListingId);
                }
            }
        }

        await context.SaveChangesAsync();
        return Ok(new { message = $"Product marked as sold on {dto.SoldOn}. Other listings flagged for removal." });
    }

    // --- Get all products with pending removals ---

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

    // --- Etsy integration ---

    [HttpPost("etsy/create-listing")]
    public async Task<ActionResult<ProductListingDto>> CreateEtsyListing([FromBody] EtsyListingRequest dto)
    {
        string? apiKey = configuration["Etsy:ApiKey"];
        string? accessToken = configuration["Etsy:AccessToken"];
        string? shopId = configuration["Etsy:ShopId"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(shopId))
        {
            return BadRequest(new { message = "Etsy is not configured. Set Etsy:ApiKey, Etsy:AccessToken, and Etsy:ShopId." });
        }

        Product? product = await context.Products.FindAsync(dto.ProductId);
        if (product is null) { return NotFound(new { message = "Product not found." }); }

        HttpClient client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        var listingData = new Dictionary<string, string>
        {
            ["quantity"] = "1",
            ["title"] = dto.Title ?? product.Name,
            ["description"] = dto.Description ?? product.Description,
            ["price"] = product.Price.ToString("F2"),
            ["who_made"] = "someone_else",
            ["when_made"] = MapEraToEtsyWhenMade(product.Era),
            ["taxonomy_id"] = "1759", // Clothing > Dresses
            ["is_supply"] = "false",
            ["type"] = "physical",
        };

        var content = new FormUrlEncodedContent(listingData);
        HttpResponseMessage response = await client.PostAsync(
            $"https://openapi.etsy.com/v3/application/shops/{shopId}/listings", content);

        string responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            string error = TryExtractError(responseJson);
            return BadRequest(new { message = $"Etsy API error: {error}" });
        }

        using JsonDocument doc = JsonDocument.Parse(responseJson);
        string listingId = doc.RootElement.GetProperty("listing_id").GetInt64().ToString();

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

    [HttpGet("etsy/status")]
    public ActionResult<object> EtsyStatus()
    {
        bool configured = !string.IsNullOrWhiteSpace(configuration["Etsy:ApiKey"])
            && !string.IsNullOrWhiteSpace(configuration["Etsy:AccessToken"])
            && !string.IsNullOrWhiteSpace(configuration["Etsy:ShopId"]);
        return Ok(new { configured });
    }

    // --- Listing text generator for Depop/Vinted ---

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

    // --- Helpers ---

    private async Task TryDeactivateEtsyListing(string listingId)
    {
        string? apiKey = configuration["Etsy:ApiKey"];
        string? accessToken = configuration["Etsy:AccessToken"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(accessToken)) { return; }

        try
        {
            HttpClient client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
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
public record GeneratedListingDto(string Title, string Description, decimal Price, string ImageUrl);
