namespace Eden_Relics_BE.Services;

public interface ICalendarFeedService
{
    /// <summary>Builds the iCal (RFC 5545) body for the operator's regulatory obligations.</summary>
    Task<string> BuildObligationsFeedAsync(CancellationToken ct);
}
