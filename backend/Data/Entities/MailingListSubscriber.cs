namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// A newsletter subscriber. People join only through a signup form (no double opt-in, by
/// decision); <see cref="BaseEntity.CreatedAtUtc"/> and <see cref="Source"/> are the record of
/// when and how they signed up. Removing someone in the admin deletes the row outright.
/// </summary>
public class MailingListSubscriber : BaseEntity, IHardDeletable
{
    public required string Email { get; set; }
    public string? FirstName { get; set; }
    public string Source { get; set; } = "Homepage"; // "Homepage", "Order Confirmation", "Discount Popup"
    public bool Unsubscribed { get; set; }
    public DateTime? UnsubscribedAtUtc { get; set; }

    /// <summary>Secret for the one-click unsubscribe link in every newsletter.</summary>
    public string? UnsubscribeToken { get; set; }
}
