using System.ComponentModel.DataAnnotations.Schema;

namespace Eden_Relics_BE.Data.Entities;

public enum ProductStatus
{
    Stock = 0,
    Live = 1,
    Sold = 2,
}

public class Product : BaseEntity
{
    public required string Name { get; set; }
    public string Slug { get; set; } = "";
    public string Sku { get; set; } = "";
    public required string Description { get; set; }
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public DateTime? StockPurchaseDate { get; set; }
    public string? Supplier { get; set; }
    public required string Era { get; set; }
    public required string Category { get; set; }
    public required string Size { get; set; }
    public required string Condition { get; set; }

    /// <summary>Primary fabric/material (e.g. "Viyella", "Rayon"). Optional; links to the
    /// matching vintage-care guide and powers "shop this fabric" cross-links.</summary>
    public string? Material { get; set; }

    public required string ImageUrl { get; set; }
    public List<string> AdditionalImageUrls { get; set; } = [];
    public List<string> VideoUrls { get; set; } = [];
    public ProductStatus Status { get; set; } = ProductStatus.Live;

    /// <summary>
    /// True when this product belongs to a curated collection. Collection members
    /// stay publicly visible after they sell (shown as "Sold" on the collection
    /// page and their own detail page) — every other sold product is hidden from
    /// the public. Set by the collection publish flow.
    /// </summary>
    public bool InCollection { get; set; }

    public decimal? SalePrice { get; set; }

    /// <summary>When the current Price was set (for 28-day reduction rule compliance)</summary>
    public DateTime? PriceSetAtUtc { get; set; }

    /// <summary>When the sale price was applied (to enforce max reduction duration)</summary>
    public DateTime? SalePriceSetAtUtc { get; set; }

    public int ViewCount { get; set; }
    public List<ProductListing> Listings { get; set; } = [];

    /// <summary>Locale -> translated name</summary>
    public Dictionary<string, string> NameTranslations { get; set; } = [];

    /// <summary>Locale -> translated description HTML</summary>
    public Dictionary<string, string> DescriptionTranslations { get; set; } = [];

    [NotMapped]
    public bool IsLive => Status == ProductStatus.Live;

    [NotMapped]
    public bool IsStock => Status == ProductStatus.Stock;

    [NotMapped]
    public bool IsSold => Status == ProductStatus.Sold;
}
