namespace Eden_Relics_BE.Data.Entities;

public class ProductListing : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public required string Platform { get; set; } // "Etsy", "Depop", "Vinted", "Website"
    public string? ExternalListingId { get; set; }
    public string? ExternalUrl { get; set; }
    public string Status { get; set; } = "Active"; // "Active", "Sold", "Removed", "PendingRemoval"
}
