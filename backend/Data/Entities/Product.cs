namespace Eden_Relics_BE.Data.Entities;

public class Product : BaseEntity
{
    public required string Name { get; set; }
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
    public bool InStock { get; set; }
    public int ViewCount { get; set; }
    public List<ProductListing> Listings { get; set; } = [];
}
