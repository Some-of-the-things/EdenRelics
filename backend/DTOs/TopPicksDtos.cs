namespace Eden_Relics_BE.DTOs;

/// <summary>
/// Public Top Picks payload. <see cref="Enabled"/> is the master gate; when false the ID lists are
/// empty (the surfaces stay dormant). The frontend resolves the product IDs against its product
/// store, so only currently-live/sold pieces render — no product detail is leaked here.
/// </summary>
public record TopPicksPublicDto(bool Enabled, List<Guid> ProductIds, List<Guid> FeaturedProductIds);

/// <summary>One curated pick: the product (by globally-unique ID) and whether it appears on the
/// homepage strip. Order is positional (the list order is the display order).</summary>
public record TopPickItemDto(Guid ProductId, bool Featured);

/// <summary>Admin view of the curated list plus the current master on/off flag.</summary>
public record TopPicksAdminDto(bool Enabled, List<TopPickItemDto> Items);

/// <summary>Replace the whole curated list, in display order.</summary>
public record SaveTopPicksRequest(List<TopPickItemDto>? Items);
