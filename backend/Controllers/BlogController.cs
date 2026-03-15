using System.Text.RegularExpressions;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public partial class BlogController(EdenRelicsDbContext context, IWebHostEnvironment env, ImageStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BlogPostSummaryDto>>> GetAll()
    {
        List<BlogPost> posts = await context.BlogPosts
            .Where(p => p.Published)
            .OrderByDescending(p => p.PublishedAtUtc)
            .ToListAsync();
        return Ok(posts.Select(ToSummaryDto).ToList());
    }

    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<BlogPostSummaryDto>>> GetAllAdmin()
    {
        List<BlogPost> posts = await context.BlogPosts
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync();
        return Ok(posts.Select(ToSummaryDto).ToList());
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<BlogPostDto>> GetBySlug(string slug)
    {
        BlogPost? post = await context.BlogPosts
            .FirstOrDefaultAsync(p => p.Slug == slug);
        if (post is null) return NotFound();
        if (!post.Published) return NotFound();
        return Ok(ToDto(post));
    }

    [HttpGet("admin/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BlogPostDto>> GetByIdAdmin(Guid id)
    {
        BlogPost? post = await context.BlogPosts.FindAsync(id);
        if (post is null) return NotFound();
        return Ok(ToDto(post));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BlogPostDto>> Create([FromBody] CreateBlogPostDto dto)
    {
        string slug = GenerateSlug(dto.Title);

        // Ensure unique slug
        int counter = 1;
        string baseSlug = slug;
        while (await context.BlogPosts.AnyAsync(p => p.Slug == slug))
        {
            slug = $"{baseSlug}-{counter++}";
        }

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
        context.BlogPosts.Add(post);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetBySlug), new { slug = post.Slug }, ToDto(post));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BlogPostDto>> Update(Guid id, [FromBody] UpdateBlogPostDto dto)
    {
        BlogPost? post = await context.BlogPosts.FindAsync(id);
        if (post is null) return NotFound();

        if (dto.Title is not null) post.Title = dto.Title;
        if (dto.Content is not null) post.Content = dto.Content;
        if (dto.Excerpt is not null) post.Excerpt = dto.Excerpt;
        if (dto.FeaturedImageUrl is not null) post.FeaturedImageUrl = dto.FeaturedImageUrl;
        if (dto.Author is not null) post.Author = dto.Author;
        if (dto.Published.HasValue)
        {
            bool wasPublished = post.Published;
            post.Published = dto.Published.Value;
            if (!wasPublished && dto.Published.Value)
                post.PublishedAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return Ok(ToDto(post));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        BlogPost? post = await context.BlogPosts.FindAsync(id);
        if (post is null) return NotFound();
        context.BlogPosts.Remove(post);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("upload-image")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> UploadImage([FromForm] IFormFile file)
    {
        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { error = "Only image files are allowed." });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "File size must be under 10MB." });

        string imageUrl = await ImageUploadHelper.ProcessAndUploadAsync(
            file.OpenReadStream(), storage, env, Request, "blog", maxWidth: 1200, maxHeight: 2000, quality: 80);
        return Ok(new { imageUrl });
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
        p.Author, p.Published, p.PublishedAtUtc, p.CreatedAtUtc
    );
}

public record BlogPostSummaryDto(Guid Id, string Title, string Slug, string? Excerpt, string? FeaturedImageUrl, string? Author, bool Published, DateTime? PublishedAtUtc, DateTime CreatedAtUtc);
public record BlogPostDto(Guid Id, string Title, string Slug, string Content, string? Excerpt, string? FeaturedImageUrl, string? Author, bool Published, DateTime? PublishedAtUtc, DateTime CreatedAtUtc);
public record CreateBlogPostDto(string Title, string Content, string? Excerpt, string? FeaturedImageUrl, string? Author, bool Published);
public record UpdateBlogPostDto(string? Title, string? Content, string? Excerpt, string? FeaturedImageUrl, string? Author, bool? Published);
