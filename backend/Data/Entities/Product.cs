namespace Eden_Relics_BE.Data.Entities;

public class Product : BaseEntity
{
    public required string Name { get; set; }
    public string Slug { get; set; } = "";
    public required string Description { get; set; }
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public string? Supplier { get; set; }
    public required string Era { get; set; }
    public required string Category { get; set; }
    public required string Size { get; set; }
    public required string Condition { get; set; }
    public required string ImageUrl { get; set; }
    public List<string> AdditionalImageUrls { get; set; } = [];
    public List<string> VideoUrls { get; set; } = [];
    public bool InStock { get; set; }
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
}
