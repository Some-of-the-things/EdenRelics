using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Background service that polls the <c>OperatorReminders</c> table every <see cref="PollInterval"/>
/// and emails anything due (via Resend). After a successful fire the reminder's <c>DueAt</c> is
/// advanced by the configured recurrence — one-shot reminders flip to inactive instead. After
/// downtime it skips missed occurrences to the next future slot rather than firing every one.
/// </summary>
public sealed class ReminderDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<ReminderDispatcher> logger) : BackgroundService
{
    /// <summary>How often to scan the queue. 5 min gives near-real-time delivery without busy-looping.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay so the dispatcher doesn't race the boot-time DB migration.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "ReminderDispatcher cycle failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task DispatchOnceAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        IEmailService email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        DateTime now = DateTime.UtcNow;
        List<OperatorReminder> due = await db.OperatorReminders
            .Where(r => r.IsActive && r.DueAt <= now)
            .ToListAsync(ct);
        if (due.Count == 0)
        {
            return;
        }

        foreach (OperatorReminder reminder in due)
        {
            try
            {
                string body = string.IsNullOrWhiteSpace(reminder.Body) ? reminder.Title : reminder.Body!;
                await email.SendOperatorReminderEmailAsync(reminder.NotifyEmail, reminder.Title, body);
                AdvanceOrDeactivate(reminder, now);
                reminder.LastNotifiedAt = now;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed firing reminder {Id} ({Title})", reminder.Id, reminder.Title);
                // Leave DueAt alone so the next poll retries.
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static void AdvanceOrDeactivate(OperatorReminder reminder, DateTime firedAt)
    {
        switch (reminder.Recurrence)
        {
            case ReminderRecurrence.None:
                reminder.IsActive = false;
                break;
            case ReminderRecurrence.Weekly:
                reminder.DueAt = AdvanceFromBaseline(reminder.DueAt, firedAt, d => d.AddDays(7));
                break;
            case ReminderRecurrence.Monthly:
                reminder.DueAt = AdvanceFromBaseline(reminder.DueAt, firedAt, d => d.AddMonths(1));
                break;
            case ReminderRecurrence.Quarterly:
                reminder.DueAt = AdvanceFromBaseline(reminder.DueAt, firedAt, d => d.AddMonths(3));
                break;
            case ReminderRecurrence.Yearly:
                reminder.DueAt = AdvanceFromBaseline(reminder.DueAt, firedAt, d => d.AddYears(1));
                break;
        }
    }

    private static DateTime AdvanceFromBaseline(DateTime due, DateTime now, Func<DateTime, DateTime> step)
    {
        DateTime next = step(due);
        while (next <= now)
        {
            next = step(next);
        }
        return next;
    }
}
