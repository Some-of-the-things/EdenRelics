using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface IAnalyticsIngestService
{
    /// <summary>
    /// Classifies a beacon and increments the matching aggregate
    /// (Date, Path, IsBot, Country) page-view counter.
    /// </summary>
    Task RecordPageViewAsync(PageViewBeaconDto beacon);
}
