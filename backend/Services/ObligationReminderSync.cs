using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Upsert for the obligation → reminder link. The reminder always carries
/// <see cref="ReminderSource.AutoFromObligation"/> so it can be distinguished from manual
/// reminders — these rows are managed by the calendar, not directly by the operator.
/// </summary>
public sealed class ObligationReminderSync(EdenRelicsDbContext db) : IObligationReminderSync
{
    public async Task SyncAsync(LiabilityObligation obligation, string notifyEmail, CancellationToken ct = default)
    {
        OperatorReminder? existing = await db.OperatorReminders
            .FirstOrDefaultAsync(r => r.LinkedObligationId == obligation.Id, ct);

        bool shouldHaveReminder =
            obligation.ScheduledFor.HasValue
            && obligation.Status != LiabilityStatus.Complete
            && obligation.Status != LiabilityStatus.Waived;

        if (!shouldHaveReminder)
        {
            if (existing is not null)
            {
                // Hard-remove the auto-row — it's not operator data we want to preserve.
                db.OperatorReminders.Remove(existing);
            }
            return;
        }

        string title = obligation.Title;
        string body = BuildBody(obligation);

        if (existing is null)
        {
            db.OperatorReminders.Add(new OperatorReminder
            {
                Title = title,
                Body = body,
                DueAt = obligation.ScheduledFor!.Value,
                Recurrence = ReminderRecurrence.None,
                NotifyEmail = notifyEmail,
                IsActive = true,
                Source = ReminderSource.AutoFromObligation,
                LinkedObligationId = obligation.Id,
            });
        }
        else
        {
            existing.Title = title;
            existing.Body = body;
            existing.DueAt = obligation.ScheduledFor!.Value;
            existing.IsActive = true;
            existing.NotifyEmail = notifyEmail;
            existing.LastNotifiedAt = null;
        }
    }

    private static string BuildBody(LiabilityObligation obligation)
    {
        string deadlineLine = $"Statutory deadline: {obligation.DueDate:dd MMM yyyy}.";
        return string.IsNullOrWhiteSpace(obligation.Notes)
            ? $"Time to do: {obligation.Title}\n{deadlineLine}"
            : $"Time to do: {obligation.Title}\n{deadlineLine}\nNotes: {obligation.Notes}";
    }
}
