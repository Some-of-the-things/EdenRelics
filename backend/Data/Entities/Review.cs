namespace Eden_Relics_BE.Data.Entities;

public class Review : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int TransactionRating { get; set; }
    public int DeliveryRating { get; set; }
    public int ProductRating { get; set; }

    public string Comment { get; set; } = string.Empty;

    public string AuthorDisplayName { get; set; } = string.Empty;

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    public DateTime? ModeratedAtUtc { get; set; }
    public Guid? ModeratedByUserId { get; set; }
    public string? ModerationNote { get; set; }
}

public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}
