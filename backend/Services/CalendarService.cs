using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Admin obligations-calendar logic. Persistence goes through the repository; the linked
/// auto-reminder is staged via <see cref="IObligationReminderSync"/> (which shares the same
/// scoped DbContext) and committed by the same save.
/// </summary>
public class CalendarService(
    IRepository<LiabilityObligation> obligations,
    ILiabilityScheduleService schedule,
    IObligationReminderSync reminders,
    IConfiguration config,
    IOptions<LiabilityOptions> options) : ICalendarService
{
    private readonly LiabilityOptions _options = options.Value;

    public async Task<List<LiabilityObligationDto>> GetAllAsync(DateOnly? from, DateOnly? to, bool openOnly, CancellationToken ct)
    {
        // Ensure the upcoming 12 months are populated before serving any list.
        await schedule.EnsureUpcomingAsync(DateTime.UtcNow, ct);

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        IQueryable<LiabilityObligation> query = obligations.Query();

        if (openOnly)
        {
            query = query.Where(o => o.Status != LiabilityStatus.Complete && o.Status != LiabilityStatus.Waived)
                .OrderBy(o => o.DueDate);
        }
        else
        {
            DateOnly fromDate = from ?? today.AddMonths(-1);
            DateOnly toDate = to ?? today.AddMonths(2);
            // Include anything whose due date OR scheduled time falls in the window.
            // timestamptz columns require UTC-kind parameters under Npgsql.
            DateTime fromTs = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            DateTime toTs = DateTime.SpecifyKind(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(o =>
                    (o.DueDate >= fromDate && o.DueDate <= toDate)
                    || (o.ScheduledFor != null && o.ScheduledFor >= fromTs && o.ScheduledFor < toTs)
                    || (o.FiledAt != null && o.FiledAt >= fromTs && o.FiledAt < toTs))
                .OrderBy(o => o.DueDate);
        }

        List<LiabilityObligation> rows = await query.ToListAsync(ct);
        return rows.Select(o => ToDto(o, today)).ToList();
    }

    public async Task<LiabilityObligationDto?> ScheduleAsync(Guid id, ScheduleObligationRequest body, CancellationToken ct)
    {
        LiabilityObligation? row = await obligations.GetByIdAsync(id);
        if (row is null) { return null; }

        row.ScheduledFor = body.ScheduledFor.ToUniversalTime();
        await reminders.SyncAsync(row, ResolveNotifyEmail(), ct);
        await obligations.UpdateAsync(row);
        return ToDto(row, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<LiabilityObligationDto?> UnscheduleAsync(Guid id, CancellationToken ct)
    {
        LiabilityObligation? row = await obligations.GetByIdAsync(id);
        if (row is null) { return null; }

        row.ScheduledFor = null;
        await reminders.SyncAsync(row, ResolveNotifyEmail(), ct);
        await obligations.UpdateAsync(row);
        return ToDto(row, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<LiabilityObligationDto?> CompleteAsync(Guid id, CompleteObligationRequest body, CancellationToken ct)
    {
        LiabilityObligation? row = await obligations.GetByIdAsync(id);
        if (row is null) { return null; }

        DateTime now = DateTime.UtcNow;
        row.Status = LiabilityStatus.Complete;
        row.FiledAt = body.FiledAt?.ToUniversalTime() ?? row.FiledAt ?? now;
        if (!string.IsNullOrWhiteSpace(body.SubmissionReference))
        {
            row.SubmissionReference = body.SubmissionReference.Trim();
        }
        if (body.PaidAmountMinor is { } amount)
        {
            row.PaidAmountMinor = amount;
            row.PaidAt = body.PaidAt?.ToUniversalTime() ?? row.PaidAt ?? now;
            if (!string.IsNullOrWhiteSpace(body.PaymentReference))
            {
                row.PaymentReference = body.PaymentReference.Trim();
            }
        }
        if (!string.IsNullOrWhiteSpace(body.Notes))
        {
            row.Notes = body.Notes.Trim();
        }
        await reminders.SyncAsync(row, ResolveNotifyEmail(), ct);
        await obligations.UpdateAsync(row);
        return ToDto(row, DateOnly.FromDateTime(now));
    }

    public async Task<LiabilityObligationDto?> WaiveAsync(Guid id, CancellationToken ct)
    {
        LiabilityObligation? row = await obligations.GetByIdAsync(id);
        if (row is null) { return null; }

        row.Status = LiabilityStatus.Waived;
        await reminders.SyncAsync(row, ResolveNotifyEmail(), ct);
        await obligations.UpdateAsync(row);
        return ToDto(row, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<LiabilityObligationDto?> ReopenAsync(Guid id, CancellationToken ct)
    {
        LiabilityObligation? row = await obligations.GetByIdAsync(id);
        if (row is null) { return null; }

        row.Status = LiabilityStatus.Pending;
        row.FiledAt = null;
        row.PaidAt = null;
        await reminders.SyncAsync(row, ResolveNotifyEmail(), ct);
        await obligations.UpdateAsync(row);
        return ToDto(row, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<LiabilityObligationDto> CreateAsync(CreateObligationRequest body, CancellationToken ct)
    {
        LiabilityObligation row = new()
        {
            Kind = LiabilityKind.Other,
            Title = body.Title.Trim(),
            PeriodStart = body.DueDate,
            PeriodEnd = body.DueDate,
            DueDate = body.DueDate,
            ScheduledFor = body.ScheduledFor?.ToUniversalTime(),
            Status = LiabilityStatus.Pending,
            Currency = "gbp",
            Notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
        };
        // First save populates Id so the reminder can FK back to it.
        await obligations.AddAsync(row);
        if (row.ScheduledFor.HasValue)
        {
            await reminders.SyncAsync(row, ResolveNotifyEmail(), ct);
            await obligations.UpdateAsync(row);
        }
        return ToDto(row, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<DeleteObligationResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        LiabilityObligation? row = await obligations.GetByIdAsync(id);
        if (row is null) { return DeleteObligationResult.NotFound; }
        if (row.Kind != LiabilityKind.Other) { return DeleteObligationResult.NotCustom; }

        // Clear the schedule first so the auto-reminder gets cleaned up, then soft-delete.
        row.ScheduledFor = null;
        row.Status = LiabilityStatus.Waived;
        await reminders.SyncAsync(row, ResolveNotifyEmail(), ct);
        await obligations.DeleteAsync(row.Id);
        return DeleteObligationResult.Deleted;
    }

    private string ResolveNotifyEmail()
    {
        if (!string.IsNullOrWhiteSpace(_options.NotifyEmail))
        {
            return _options.NotifyEmail!;
        }
        return config["Email:SaleNotificationRecipient"] ?? "orionsaxis@gmail.com";
    }

    private static LiabilityObligationDto ToDto(LiabilityObligation o, DateOnly today)
    {
        bool overdue = o.Status == LiabilityStatus.Pending && o.DueDate < today;
        return new LiabilityObligationDto(
            o.Id, o.Kind, o.Title, o.PeriodStart, o.PeriodEnd, o.DueDate, o.Status, overdue,
            o.ScheduledFor, o.FiledAt, o.SubmissionReference, o.OwedAmountMinor, o.Currency,
            o.PaidAt, o.PaidAmountMinor, o.PaymentReference, o.Notes);
    }
}
