namespace Eden_Relics_BE.DTOs;

/// <summary>
/// Cookieless server-to-server beacon sent by the Cloudflare Worker on each SSR render.
/// No cookies, no client identifiers — just enough to aggregate.
/// </summary>
public record PageViewBeaconDto(
    string? Path,
    string? Country,
    string? UserAgent,
    string? AsOrganization);
