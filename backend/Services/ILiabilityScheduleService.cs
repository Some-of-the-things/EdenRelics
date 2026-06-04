namespace Eden_Relics_BE.Services;

/// <summary>
/// Generates the next 12 months of <c>LiabilityObligation</c> rows from the known catalogue of
/// UK statutory deadlines. Idempotent: skips any (Kind, PeriodEnd) pair that already exists.
/// </summary>
public interface ILiabilityScheduleService
{
    /// <summary>Generate any missing obligations whose due date falls within the next 12 months from <paramref name="asOf"/>. Returns the count inserted.</summary>
    Task<int> EnsureUpcomingAsync(DateTime asOf, CancellationToken ct = default);
}
