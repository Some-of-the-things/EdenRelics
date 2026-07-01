namespace Eden_Relics_BE.DTOs;

/// <summary>Request to fetch preview data for a set of products by SKU (may include Stock).</summary>
public record CollectionPreviewRequest(List<string>? Skus);

/// <summary>Slim product shape for the collection preview/approval page. Deliberately omits
/// cost price, supplier and other internal fields — the preview token is shared with a
/// non-admin reviewer.</summary>
public record CollectionPreviewProductDto(
    string Sku,
    string Name,
    string Slug,
    decimal Price,
    decimal? SalePrice,
    string ImageUrl,
    bool IsLive);

/// <summary>One product to publish: flip to Live and (optionally) set a clean slug.</summary>
public record CollectionPublishItem(string? Sku, string? Slug);

public record CollectionPublishRequest(List<CollectionPublishItem>? Items);

public record CollectionPublishResult(int Published, int AlreadyLive, List<string> NotFound);
