using System.Text.RegularExpressions;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Blog domain logic. Controllers stay thin and never touch the DbContext directly;
/// all persistence goes through <see cref="IRepository{T}"/>, whose DeleteAsync is a
/// soft delete (and is further guarded by the global SoftDeleteInterceptor).
/// </summary>
public partial class BlogService(IRepository<BlogPost> repository) : IBlogService
{
    public async Task<List<BlogPostSummaryDto>> GetPublishedAsync()
    {
        IEnumerable<BlogPost> posts = await repository.FindAsync(p => p.Published);
        return posts
            .OrderByDescending(p => p.PublishedAtUtc)
            .Select(ToSummaryDto)
            .ToList();
    }

    public async Task<List<BlogPostSummaryDto>> GetAllForAdminAsync()
    {
        IEnumerable<BlogPost> posts = await repository.GetAllAsync();
        return posts
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(ToSummaryDto)
            .ToList();
    }

    public async Task<BlogPostDto?> GetPublishedBySlugAsync(string slug)
    {
        BlogPost? post = (await repository.FindAsync(p => p.Slug == slug)).FirstOrDefault();
        if (post is null || !post.Published)
        {
            return null;
        }
        return ToDto(post);
    }

    public async Task<BlogPostDto?> GetBySlugForAdminAsync(string slug)
    {
        BlogPost? post = (await repository.FindAsync(p => p.Slug == slug)).FirstOrDefault();
        return post is null ? null : ToDto(post);
    }

    public async Task<BlogPostDto?> GetByIdForAdminAsync(Guid id)
    {
        BlogPost? post = await repository.GetByIdAsync(id);
        return post is null ? null : ToDto(post);
    }

    public async Task<BlogPostDto> CreateAsync(CreateBlogPostDto dto)
    {
        string slug = await GenerateUniqueSlugAsync(dto.Title);

        BlogPost post = new()
        {
            Title = dto.Title,
            Slug = slug,
            Content = dto.Content,
            Excerpt = dto.Excerpt,
            FeaturedImageUrl = dto.FeaturedImageUrl,
            Author = dto.Author,
            Published = dto.Published,
            PublishedAtUtc = dto.Published ? DateTime.UtcNow : null,
        };

        await repository.AddAsync(post);
        return ToDto(post);
    }

    public async Task<BlogPostDto?> UpdateAsync(Guid id, UpdateBlogPostDto dto)
    {
        BlogPost? post = await repository.GetByIdAsync(id);
        if (post is null)
        {
            return null;
        }

        if (dto.Title is not null) { post.Title = dto.Title; }
        if (dto.Content is not null) { post.Content = dto.Content; }
        if (dto.Excerpt is not null) { post.Excerpt = dto.Excerpt; }
        if (dto.FeaturedImageUrl is not null) { post.FeaturedImageUrl = dto.FeaturedImageUrl; }
        if (dto.Author is not null) { post.Author = dto.Author; }
        if (dto.Published.HasValue)
        {
            bool wasPublished = post.Published;
            post.Published = dto.Published.Value;
            if (!wasPublished && dto.Published.Value)
            {
                post.PublishedAtUtc = DateTime.UtcNow;
            }
        }

        await repository.UpdateAsync(post);
        return ToDto(post);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        BlogPost? post = await repository.GetByIdAsync(id);
        if (post is null)
        {
            return false;
        }

        await repository.DeleteAsync(id);
        return true;
    }

    private async Task<string> GenerateUniqueSlugAsync(string title)
    {
        string baseSlug = GenerateSlug(title);
        string slug = baseSlug;
        int counter = 1;

        // includeDeleted: the Slug unique index covers soft-deleted rows too, so a
        // deleted post still reserves its slug and we must skip past it.
        while ((await repository.FindAsync(p => p.Slug == slug, includeDeleted: true)).Any())
        {
            slug = $"{baseSlug}-{counter++}";
        }

        return slug;
    }

    private static string GenerateSlug(string title)
    {
        string slug = title.ToLowerInvariant();
        slug = SlugInvalidChars().Replace(slug, "");
        slug = SlugWhitespace().Replace(slug, "-");
        slug = SlugMultipleDashes().Replace(slug, "-");
        return slug.Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex SlugInvalidChars();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SlugWhitespace();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex SlugMultipleDashes();

    private static BlogPostSummaryDto ToSummaryDto(BlogPost p) => new(
        p.Id, p.Title, p.Slug, p.Excerpt, p.FeaturedImageUrl, p.Author,
        p.Published, p.PublishedAtUtc, p.CreatedAtUtc
    );

    private static BlogPostDto ToDto(BlogPost p) => new(
        p.Id, p.Title, p.Slug, p.Content, p.Excerpt, p.FeaturedImageUrl,
        p.Author, p.Published, p.PublishedAtUtc, p.CreatedAtUtc, p.UpdatedAtUtc
    );
}
