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
