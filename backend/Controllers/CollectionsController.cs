using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

/// <summary>
/// Backs the admin-gated "review then publish" flow for a curated product collection.
/// An admin opens the preview page (/collections/preview/:slug), sees the pieces — including
/// ones still held as Stock — and clicks Approve to make them Live.
///
/// Both endpoints require the Admin role (same gate as product create/update). The publish
/// action only ever flips products that are currently Stock to Live (it never touches Live or
/// Sold), so approval can't accidentally hide or resurrect anything — only bring stock forward.
/// </summary>
[ApiController]
[Route("api/collections")]
[Authorize(Roles = "Admin")]
public class CollectionsController(
    IRepository<Product> products,
    ILogger<CollectionsController> logger) : ControllerBase
{
    /// <summary>Preview data for the given SKUs, including products still held as Stock.</summary>
    [HttpPost("preview-products")]
    public async Task<IActionResult> PreviewProducts([FromBody] CollectionPreviewRequest request)
    {
        string[] skus = (request.Skus ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        if (skus.Length == 0)
        {
            return Ok(Array.Empty<CollectionPreviewProductDto>());
        }

        IEnumerable<Product> found = await products.FindAsync(p => skus.Contains(p.Sku));
        Dictionary<string, Product> bySku = found
            .Where(p => !string.IsNullOrEmpty(p.Sku))
            .ToDictionary(p => p.Sku, p => p);

        // Preserve the requested (curated) order.
        List<CollectionPreviewProductDto> ordered = skus
            .Where(bySku.ContainsKey)
            .Select(sku => bySku[sku])
            .Select(p => new CollectionPreviewProductDto(
                p.Sku,
                p.Name,
                string.IsNullOrEmpty(p.Slug) ? p.Id.ToString() : p.Slug,
                p.Price,
                p.SalePrice,
                p.ImageUrl,
                p.Status == ProductStatus.Live))
            .ToList();

        return Ok(ordered);
    }

    /// <summary>Approve &amp; publish: flip each still-Stock product to Live, setting a clean slug.</summary>
    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] CollectionPublishRequest request)
    {
        List<CollectionPublishItem> items = (request.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Sku))
            .ToList();
        if (items.Count == 0)
        {
            return BadRequest("No products to publish.");
        }

        string[] skus = items.Select(i => i.Sku!).ToArray();
        Dictionary<string, Product> bySku = (await products.FindAsync(p => skus.Contains(p.Sku)))
            .Where(p => !string.IsNullOrEmpty(p.Sku))
            .ToDictionary(p => p.Sku, p => p);

        int published = 0;
        int alreadyLive = 0;
        List<string> notFound = [];

        foreach (CollectionPublishItem item in items)
        {
            if (!bySku.TryGetValue(item.Sku!, out Product? product))
            {
                notFound.Add(item.Sku!);
                continue;
            }

            bool changed = false;

            // Mark collection membership so the piece stays publicly visible once it
            // sells (backfills already-live members too). Idempotent.
            if (!product.InCollection)
            {
                product.InCollection = true;
                changed = true;
            }

            if (product.Status == ProductStatus.Stock)
            {
                if (!string.IsNullOrWhiteSpace(item.Slug))
                {
                    product.Slug = item.Slug!;
                }
                product.Status = ProductStatus.Live;
                published++;
                changed = true;
            }
            else if (product.Status == ProductStatus.Live)
            {
                alreadyLive++;
            }
            // Sold members are left Sold (never resurrected) but still get the flag above.

            if (changed)
            {
                await products.UpdateAsync(product);
            }
        }

        logger.LogInformation(
            "Collection publish: {Published} published, {AlreadyLive} already live, {NotFound} not found.",
            published, alreadyLive, notFound.Count);

        return Ok(new CollectionPublishResult(published, alreadyLive, notFound));
    }
}
