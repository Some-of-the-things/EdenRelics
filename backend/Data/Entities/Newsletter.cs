namespace Eden_Relics_BE.Data.Entities;

public enum NewsletterStatus
{
    Draft = 0,
    Scheduled = 1,
    Sending = 2,
    Sent = 3,
}

/// <summary>
/// A newsletter written in the admin. The body is stored as written (already cleaned of scripts
/// on save); the Eden Relics header and footer are wrapped round it, and the CSS inlined, at send
/// time, so a template change applies to drafts too.
/// </summary>
public class Newsletter : BaseEntity
{
    public string Subject { get; set; } = string.Empty;

    /// <summary>The grey line inboxes show after the subject.</summary>
    public string PreviewText { get; set; } = string.Empty;

    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>"visual" or "html": which editor the draft was last edited in.</summary>
    public string EditorMode { get; set; } = "visual";

    public NewsletterStatus Status { get; set; } = NewsletterStatus.Draft;

    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? SendStartedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }

    /// <summary>Active subscribers at the moment sending started.</summary>
    public int RecipientCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>Recipients skipped because they unsubscribed between the send starting and their batch going out.</summary>
    public int SkippedCount { get; set; }

    /// <summary>The next batch number to hand out. Batch numbers make the provider idempotency key, so a retried batch is never sent twice.</summary>
    public int NextBatchNumber { get; set; }

    public string? LastError { get; set; }
}

public enum NewsletterDeliveryStatus
{
    Pending = 0,
    /// <summary>Claimed by a batch that has been (or is being) handed to the provider.</summary>
    Sending = 1,
    Sent = 2,
    Failed = 3,
    /// <summary>Unsubscribed after the send started; never sent.</summary>
    Skipped = 4,
}

/// <summary>
/// One recipient of one newsletter. Rows are created for every active subscriber when sending
/// starts (a snapshot of the list), then worked through in batches. A batch keeps its number
/// across retries so the provider's idempotency key deduplicates a retry of a batch whose
/// response was lost; nobody is skipped or sent twice.
/// </summary>
public class NewsletterDelivery : BaseEntity, IHardDeletable
{
    public Guid NewsletterId { get; set; }
    public Guid SubscriberId { get; set; }
    public string Email { get; set; } = string.Empty;
    public NewsletterDeliveryStatus Status { get; set; } = NewsletterDeliveryStatus.Pending;
    public int? BatchNumber { get; set; }
    public int Attempts { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? Error { get; set; }

    /// <summary>The provider's id for this email, so open/click/bounce events can be matched back.</summary>
    public string? ProviderId { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime? ClickedAtUtc { get; set; }
    public DateTime? BouncedAtUtc { get; set; }
}

/// <summary>Simple admin-editable settings, one row per key.</summary>
public class AppSetting : BaseEntity, IHardDeletable
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
