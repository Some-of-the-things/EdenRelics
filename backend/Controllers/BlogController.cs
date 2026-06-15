using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlogController(IBlogService blog, IWebHostEnvironment env, ImageStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BlogPostSummaryDto>>> GetAll()
    {
        return Ok(await blog.GetPublishedAsync());
    }

    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<BlogPostSummaryDto>>> GetAllAdmin()
    {
        return Ok(await blog.GetAllForAdminAsync());
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<BlogPostDto>> GetBySlug(string slug)
    {
        BlogPostDto? post = await blog.GetPublishedBySlugAsync(slug);
        return post is null ? NotFound() : Ok(post);
    }

    /// <summary>
    /// Admin-only preview: returns a post by slug whether or not it's published,
    /// so drafts can be reviewed in the real blog layout before going live.
    /// </summary>
    [HttpGet("preview/{slug}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BlogPostDto>> PreviewBySlug(string slug)
    {
        BlogPostDto? post = await blog.GetBySlugForAdminAsync(slug);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpGet("admin/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BlogPostDto>> GetByIdAdmin(Guid id)
    {
        BlogPostDto? post = await blog.GetByIdForAdminAsync(id);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BlogPostDto>> Create([FromBody] CreateBlogPostDto dto)
    {
        BlogPostDto post = await blog.CreateAsync(dto);
        return CreatedAtAction(nameof(GetBySlug), new { slug = post.Slug }, post);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BlogPostDto>> Update(Guid id, [FromBody] UpdateBlogPostDto dto)
    {
        BlogPostDto? post = await blog.UpdateAsync(id, dto);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        bool deleted = await blog.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("upload-image")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(long.MaxValue)]
    public async Task<ActionResult<object>> UploadImage([FromForm] IFormFile file)
    {
        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Only image files are allowed." });
        }

        if (file.Length > UploadLimits.MaxUploadBytes)
        {
            return BadRequest(new { error = $"File size must be under {UploadLimits.MaxUploadDisplay}." });
        }

        string imageUrl = await ImageUploadHelper.ProcessAndUploadAsync(
            file.OpenReadStream(), storage, env, Request, "blog",
            ImageUploadHelper.DefaultVariantWidths, quality: 80);
        return Ok(new { imageUrl });
    }
}
