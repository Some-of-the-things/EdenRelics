using Eden_Relics_BE.Data.Entities;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Keeps the obligation → auto-reminder link in sync. Stages the reminder add/update/remove on
/// the shared DbContext; the caller is responsible for SaveChanges.
/// </summary>
public interface IObligationReminderSync
{
    Task SyncAsync(LiabilityObligation obligation, string notifyEmail, CancellationToken ct = default);
}
