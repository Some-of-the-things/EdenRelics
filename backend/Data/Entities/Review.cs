namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// A review of a website order, left by the buyer. One per order. Reviews publish straight away
/// (UK DMCC Act / CMA guidance: no cherry-picking, no unreasonable delay); only ones the filter
/// flags (links, contact details, abuse) wait for a check. Hiding needs a recorded reason.
/// </summary>
public class Review : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    /// <summary>The buyer's account, or null for a guest order reviewed through the emailed link.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Stars, 1 to 5.</summary>
    public int Rating { get; set; }

    // The original three-part ratings. New reviews have one star rating, written to all three so
    // older readers of these columns keep working.
    public int TransactionRating { get; set; }
    public int DeliveryRating { get; set; }
    public int ProductRating { get; set; }

    public string Comment { get; set; } = string.Empty;

    public string AuthorDisplayName { get; set; } = string.Empty;

    /// <summary>Optional photo of the buyer wearing or styling the piece.</summary>
    public string? PhotoUrl { get; set; }

    /// <summary>The shop's public reply.</summary>
    public string? Reply { get; set; }
    public DateTime? RepliedAtUtc { get; set; }

    /// <summary>Approved = published; Pending = held by the filter for a check; Rejected = hidden.</summary>
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    /// <summary>Why the filter held it, when it did.</summary>
    public string? HoldReason { get; set; }

    /// <summary>Set when hidden: one of <see cref="ReviewHideReasons"/>.</summary>
    public string? HiddenReason { get; set; }

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

/// <summary>The only grounds for hiding a review (from the brief and the review policy).</summary>
public static class ReviewHideReasons
{
    public const string Abuse = "abuse";
    public const string PersonalData = "personal-data";
    public const string Spam = "spam";
    public const string NotAboutPurchase = "not-about-purchase";

    public static readonly IReadOnlyList<string> All = [Abuse, PersonalData, Spam, NotAboutPurchase];
}
