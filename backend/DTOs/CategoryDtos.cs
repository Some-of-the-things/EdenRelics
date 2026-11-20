namespace Eden_Relics_BE.DTOs;

public record SubCategoryDto(
    Guid Id,
    string HubKey,
    string Slug,
    string Name,
    string Tagline,
    string Intro,
    string Body,
    string MetaTitle,
    string MetaDescription,
    int DisplayOrder,
    bool IsVisible,
    List<string> IncludeKeywords,
    List<string> ExcludeKeywords,
    string? ContentKey,
    /// <summary>Pieces added by hand although their name doesn't match.</summary>
    List<Guid> AddedProductIds,
    /// <summary>Pieces kept out by hand although their name matches.</summary>
    List<Guid> RemovedProductIds);

public record CategoryHubDto(string Key, string Name, List<SubCategoryDto> SubCategories);

public record CategoryRedirectDto(string HubKey, string FromSlug, string ToPath);

public record HiddenSubCategoryDto(string HubKey, string Slug);

/// <summary>
/// Everything the public site needs to render hubs, sub-category pages and product chips, and to
/// send old or hidden sub-category URLs somewhere sensible.
/// </summary>
public record CategoriesDto(
    List<CategoryHubDto> Hubs,
    List<CategoryRedirectDto> Redirects,
    List<HiddenSubCategoryDto> Hidden);

public record SaveSubCategoryRequest(
    string? HubKey,
    string? Name,
    string? Slug,
    string? Tagline,
    string? Intro,
    string? Body,
    string? MetaTitle,
    string? MetaDescription,
    bool IsVisible,
    List<string>? IncludeKeywords,
    List<string>? ExcludeKeywords);

public record ReorderSubCategoriesRequest(List<Guid>? Ids);

public record SetSubCategoryProductsRequest(List<Guid>? Add, List<Guid>? Remove);

public record SetProductCategoriesRequest(List<Guid>? SubCategoryIds);
