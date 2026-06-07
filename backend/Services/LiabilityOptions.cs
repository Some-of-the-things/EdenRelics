namespace Eden_Relics_BE.Services;

/// <summary>
/// Configuration for the regulatory-obligations calendar. Bound from the <c>Liabilities</c>
/// section of <c>appsettings.json</c> with overrides from environment / Fly secrets in higher
/// environments.
/// </summary>
public sealed class LiabilityOptions
{
    public const string SectionName = "Liabilities";

    /// <summary>
    /// The company's Accounting Reference Date as <c>MM-DD</c> (e.g. <c>05-31</c> for 31 May).
    /// When null/empty, year-end-anchored obligations (CT600, CT payment, statutory accounts)
    /// are skipped and the UI shows a banner asking the operator to set it.
    /// </summary>
    public string? AccountingReferenceDate { get; set; }

    /// <summary>
    /// Day-of-year (<c>MM-DD</c>) the Confirmation Statement is due. Anchored to the company's
    /// made-up date at Companies House, NOT the ARD. Default null.
    /// </summary>
    public string? ConfirmationStatementMadeUpDate { get; set; }

    /// <summary>
    /// HMRC VAT stagger group: 1 = quarters end Mar/Jun/Sep/Dec, 2 = Apr/Jul/Oct/Jan,
    /// 3 = May/Aug/Nov/Feb. Defaults to 1.
    /// </summary>
    public int VatStagger { get; set; } = 1;

    /// <summary>
    /// Email address the calendar's auto-reminders are sent to. Falls back to the
    /// <c>Email:SaleNotificationRecipient</c> address when null/empty.
    /// </summary>
    public string? NotifyEmail { get; set; }

    /// <summary>
    /// Secret token for the public iCal feed (<c>/api/calendar/obligations.ics?token=…</c>).
    /// When null/empty the feed endpoint returns 404 — set this to enable Google/Outlook/iPhone subscribe.
    /// </summary>
    public string? IcalToken { get; set; }
}
