using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface ICalendarService
{
    Task<List<LiabilityObligationDto>> GetAllAsync(DateOnly? from, DateOnly? to, bool openOnly, CancellationToken ct);
    Task<LiabilityObligationDto?> ScheduleAsync(Guid id, ScheduleObligationRequest body, CancellationToken ct);
    Task<LiabilityObligationDto?> UnscheduleAsync(Guid id, CancellationToken ct);
    Task<LiabilityObligationDto?> CompleteAsync(Guid id, CompleteObligationRequest body, CancellationToken ct);
    Task<LiabilityObligationDto?> WaiveAsync(Guid id, CancellationToken ct);
    Task<LiabilityObligationDto?> ReopenAsync(Guid id, CancellationToken ct);
    Task<LiabilityObligationDto> CreateAsync(CreateObligationRequest body, CancellationToken ct);
    Task<DeleteObligationResult> DeleteAsync(Guid id, CancellationToken ct);
}

public enum DeleteObligationResult { NotFound, NotCustom, Deleted }
