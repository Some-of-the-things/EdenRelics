namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// Recurrence cadence for an operator reminder. <c>None</c> = one-shot (auto-deactivates
/// after firing); everything else advances <c>DueAt</c> by the interval and stays active.
/// </summary>
public enum ReminderRecurrence
{
    None = 0,
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4,
}

/// <summary>
/// Whether the reminder was created manually by the operator or auto-generated as the
/// work-session notification for a <see cref="LiabilityObligation"/>. Auto rows are cleaned
/// up when their owning obligation is marked complete / waived / rescheduled.
/// </summary>
public enum ReminderSource
{
    Manual = 0,
    AutoFromObligation = 1,
}

/// <summary>
/// A scheduled operator reminder that fires an email via Resend when <c>DueAt</c> passes.
/// Used as the work-session notification for calendar obligations. Dispatched by the
/// <c>ReminderDispatcher</c> hosted service, which polls every few minutes and updates
/// <c>DueAt</c>/<c>LastNotifiedAt</c> after firing.
/// </summary>
// Auto-generated from obligations and rebuilt rather than preserved, so hard-deleted.
public class OperatorReminder : BaseEntity, IHardDeletable
{
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }

    /// <summary>When this reminder should next fire. Updated after each successful dispatch.</summary>
    public DateTime DueAt { get; set; }

    public ReminderRecurrence Recurrence { get; set; }

    /// <summary>The email address the reminder is sent to.</summary>
    public string NotifyEmail { get; set; } = string.Empty;

    /// <summary>Last successful dispatch timestamp, or null if the reminder has never fired.</summary>
    public DateTime? LastNotifiedAt { get; set; }

    /// <summary>Whether this reminder is in the dispatch queue. Deactivated on delete or after a one-shot fires.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Origin of the reminder. Manual = operator-created; auto rows belong to an obligation and are managed by the scheduler.</summary>
    public ReminderSource Source { get; set; } = ReminderSource.Manual;

    /// <summary>For <c>Source = AutoFromObligation</c> rows, the id of the obligation this reminder belongs to.</summary>
    public Guid? LinkedObligationId { get; set; }
}
