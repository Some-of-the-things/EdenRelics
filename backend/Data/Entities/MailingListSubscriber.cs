namespace Eden_Relics_BE.Data.Entities;

public class MailingListSubscriber : BaseEntity
{
    public required string Email { get; set; }
    public string? FirstName { get; set; }
    public string Source { get; set; } = "Homepage"; // "Homepage", "Order Confirmation"
    public bool Unsubscribed { get; set; }
}
