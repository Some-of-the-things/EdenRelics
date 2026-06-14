namespace Eden_Relics_BE.Services;

public interface IBlogService
{
    Task<List<BlogPostSummaryDto>> GetPublishedAsync();
    Task<List<BlogPostSummaryDto>> GetAllForAdminAsync();
    Task<BlogPostDto?> GetPublishedBySlugAsync(string slug);
    Task<BlogPostDto?> GetBySlugForAdminAsync(string slug);
    Task<BlogPostDto?> GetByIdForAdminAsync(Guid id);
    Task<BlogPostDto> CreateAsync(CreateBlogPostDto dto);
    Task<BlogPostDto?> UpdateAsync(Guid id, UpdateBlogPostDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public record BlogPostSummaryDto(Guid Id, string Title, string Slug, string? Excerpt, string? FeaturedImageUrl, string? Author, bool Published, DateTime? PublishedAtUtc, DateTime CreatedAtUtc);
public record BlogPostDto(Guid Id, string Title, string Slug, string Content, string? Excerpt, string? FeaturedImageUrl, string? Author, bool Published, DateTime? PublishedAtUtc, DateTime CreatedAtUtc);
public record CreateBlogPostDto(string Title, string Content, string? Excerpt, string? FeaturedImageUrl, string? Author, bool Published);
public record UpdateBlogPostDto(string? Title, string? Content, string? Excerpt, string? FeaturedImageUrl, string? Author, bool? Published);
