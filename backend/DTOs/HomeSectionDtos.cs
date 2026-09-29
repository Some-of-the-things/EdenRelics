namespace Eden_Relics_BE.DTOs;

/// <summary>
/// A homepage product strip. Order is positional: the list order is the display order.
/// <see cref="ProductIds"/> is only meaningful for custom sections; the built-in sections take
/// their pieces from Top Picks and from the collection profile.
/// </summary>
public record HomeSectionDto(
    string Kind,
    string Eyebrow,
    string Title,
    string Description,
    string ButtonText,
    string ButtonLink,
    bool Visible,
    List<Guid> ProductIds);

/// <summary>Replace every homepage section, in display order.</summary>
public record SaveHomeSectionsRequest(List<HomeSectionDto>? Sections);
