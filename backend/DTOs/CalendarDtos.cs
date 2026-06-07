using Eden_Relics_BE.Data.Entities;

namespace Eden_Relics_BE.DTOs;

/// <summary>One regulatory obligation / calendar event, shaped for the admin UI.</summary>
public record LiabilityObligationDto(
    Guid Id,
    LiabilityKind Kind,
    string Title,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    LiabilityStatus Status,
    bool IsOverdue,
    DateTime? ScheduledFor,
    DateTime? FiledAt,
    string? SubmissionReference,
    long? OwedAmountMinor,
    string Currency,
    DateTime? PaidAt,
    long? PaidAmountMinor,
    string? PaymentReference,
    string? Notes);

/// <summary>Whether the calendar is configured (ARD set) and whether the iCal feed is enabled.</summary>
public record CalendarConfigDto(bool ArdConfigured, bool IcalEnabled, string? IcalSubscribeUrl);

/// <summary>Assign a work-session time to an obligation.</summary>
public record ScheduleObligationRequest(DateTime ScheduledFor);

/// <summary>Mark an obligation done with completion evidence.</summary>
public record CompleteObligationRequest(
    string? SubmissionReference,
    long? PaidAmountMinor,
    string? PaymentReference,
    DateTime? PaidAt,
    DateTime? FiledAt,
    string? Notes);

/// <summary>Create a free-form calendar event (Kind = Other).</summary>
public record CreateObligationRequest(
    string Title,
    DateOnly DueDate,
    DateTime? ScheduledFor,
    string? Notes);
