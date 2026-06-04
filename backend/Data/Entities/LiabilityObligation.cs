namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// What kind of statutory / regulatory liability this is. Drives the auto-generation cadence
/// (the schedule service maps each kind to its filing/payment cycle) and shapes the evidence
/// fields the operator captures when marking complete. <see cref="Other"/> is reserved for
/// free-form operator-authored events — the scheduler never produces those.
/// </summary>
public enum LiabilityKind
{
    UkVatReturn = 1,
    UkVatPayment = 2,
    PayeRti = 3,
    EuOssReturn = 10,
    P60Issuance = 20,
    P11dFiling = 21,
    SelfAssessment = 22,
    CorporationTaxPayment = 30,
    CorporationTaxReturn = 31,
    StatutoryAccounts = 32,
    ConfirmationStatement = 33,
    Other = 99,
}

/// <summary>
/// Lifecycle state of a single obligation instance. Overdue is computed in the API layer
/// from <c>Status == Pending &amp;&amp; DueDate &lt; today</c>; it is not stored.
/// </summary>
public enum LiabilityStatus
{
    /// <summary>Auto-created, nothing done yet.</summary>
    Pending = 0,
    /// <summary>Filed with HMRC / Companies House but payment (if any) not yet made.</summary>
    Submitted = 1,
    /// <summary>Money paid but filing not yet confirmed (rare ordering).</summary>
    Paid = 2,
    /// <summary>Fully done — filed and (if applicable) paid, with evidence captured.</summary>
    Complete = 3,
    /// <summary>Operator marked N/A (e.g. nil return filed elsewhere, obligation not actually owed this period).</summary>
    Waived = 4,
}

/// <summary>
/// One instance of a regulatory liability the operator has to meet, e.g. "UK VAT Return for
/// Q1 2026, due 7 May 2026". Statutory rows are auto-generated 12 months ahead by
/// <c>LiabilityScheduleService</c>; <see cref="LiabilityKind.Other"/> rows are free-form events
/// the operator adds by hand. The operator schedules when they'll do the work
/// (<see cref="ScheduledFor"/>) and records completion evidence (<see cref="SubmissionReference"/>,
/// <see cref="PaymentReference"/>, timestamps).
///
/// <para>Scheduling an obligation creates a linked <see cref="OperatorReminder"/> (keyed back via
/// <see cref="OperatorReminder.LinkedObligationId"/>) that emails the operator at
/// <see cref="ScheduledFor"/>; it is cancelled when the obligation is completed, waived or
/// rescheduled.</para>
/// </summary>
public class LiabilityObligation : BaseEntity
{
    public LiabilityKind Kind { get; set; }

    /// <summary>Human-friendly title shown in the calendar — e.g. "UK VAT Return — Q1 2026".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>First day of the period this obligation covers (inclusive).</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Last day of the period this obligation covers (inclusive).</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Statutory deadline. After this date a Pending row is considered overdue.</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>Last action recorded on this obligation. Overdue is computed, not stored.</summary>
    public LiabilityStatus Status { get; set; } = LiabilityStatus.Pending;

    /// <summary>When the operator plans to actually do the work. Drives the auto-reminder.</summary>
    public DateTime? ScheduledFor { get; set; }

    /// <summary>When the filing was made (or the payment, if filing-only).</summary>
    public DateTime? FiledAt { get; set; }

    /// <summary>HMRC MTD submission ID, Companies House reference, OSS portal reference, etc.</summary>
    public string? SubmissionReference { get; set; }

    /// <summary>Amount owed in minor units (e.g. 457200 = £4,572.00). Null for non-monetary filings (CS01, P60 issue).</summary>
    public long? OwedAmountMinor { get; set; }

    /// <summary>ISO 4217 currency code, lowercased. Defaults to "gbp" — OSS uses "eur".</summary>
    public string Currency { get; set; } = "gbp";

    /// <summary>When payment was made.</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>Amount actually paid (may differ from <see cref="OwedAmountMinor"/> for true-ups).</summary>
    public long? PaidAmountMinor { get; set; }

    /// <summary>Bank reference / Faster Payments ref / direct-debit reference for the payment.</summary>
    public string? PaymentReference { get; set; }

    /// <summary>Operator notes — context, gotchas, links to supporting documents.</summary>
    public string? Notes { get; set; }
}
