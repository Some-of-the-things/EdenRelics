namespace Eden_Relics_BE.Data.Entities;

public class Order : BaseEntity
{
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string? GuestEmail { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal Total { get; set; }
    public string? StripeSessionId { get; set; }
    public List<OrderItem> Items { get; set; } = [];

    // Shipping
    public string? ShippingMethod { get; set; }
    public decimal ShippingCost { get; set; }

    // Shipping address
    public string? ShipAddressLine1 { get; set; }
    public string? ShipAddressLine2 { get; set; }
    public string? ShipCity { get; set; }
    public string? ShipCounty { get; set; }
    public string? ShipPostcode { get; set; }
    public string? ShipCountry { get; set; }

    // Billing address
    public string? BillAddressLine1 { get; set; }
    public string? BillAddressLine2 { get; set; }
    public string? BillCity { get; set; }
    public string? BillCounty { get; set; }
    public string? BillPostcode { get; set; }
    public string? BillCountry { get; set; }
}

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
