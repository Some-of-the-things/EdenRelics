using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IRepository<Product> _repository;
    private readonly IWebHostEnvironment _env;

    public ProductsController(IRepository<Product> repository, IWebHostEnvironment env)
    {
        _repository = repository;
        _env = env;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll()
    {
        IEnumerable<Product> products = await _repository.GetAllAsync();
        return Ok(products.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id)
    {
        Product? product = await _repository.GetByIdAsync(id);
        if (product is null) return NotFound();
        return Ok(ToDto(product));
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetByCategory(string category)
    {
        IEnumerable<Product> products = await _repository.FindAsync(p => p.Category == category);
        return Ok(products.Select(ToDto));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create(CreateProductDto dto)
    {
        Product product = new()
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Era = dto.Era,
            Category = dto.Category,
            Size = dto.Size,
            Condition = dto.Condition,
            ImageUrl = dto.ImageUrl,
            InStock = dto.InStock
        };

        await _repository.AddAsync(product);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, ToDto(product));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpdateProductDto dto)
    {
        Product? product = await _repository.GetByIdAsync(id);
        if (product is null) return NotFound();

        if (dto.Name is not null) product.Name = dto.Name;
        if (dto.Description is not null) product.Description = dto.Description;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.Era is not null) product.Era = dto.Era;
        if (dto.Category is not null) product.Category = dto.Category;
        if (dto.Size is not null) product.Size = dto.Size;
        if (dto.Condition is not null) product.Condition = dto.Condition;
        if (dto.ImageUrl is not null) product.ImageUrl = dto.ImageUrl;
        if (dto.InStock.HasValue) product.InStock = dto.InStock.Value;

        await _repository.UpdateAsync(product);
        return Ok(ToDto(product));
    }

    [HttpPost("upload-image")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> UploadImage([FromForm] IFormFile file)
    {
        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Only image files (jpg, png, webp, gif) are allowed." });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { error = "File size must be under 10MB." });
        }

        string uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
        Directory.CreateDirectory(uploadsDir);

        string fileName = $"{Guid.NewGuid()}.webp";
        string filePath = Path.Combine(uploadsDir, fileName);

        using var image = await Image.LoadAsync(file.OpenReadStream());
        image.Mutate(x => x.AutoOrient());

        const int maxWidth = 800;
        const int maxHeight = 1000;
        if (image.Width > maxWidth || image.Height > maxHeight)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxWidth, maxHeight),
                Mode = ResizeMode.Max,
            }));
        }

        await image.SaveAsync(filePath, new WebpEncoder { Quality = 75 });

        string imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
        return Ok(new { imageUrl });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        Product? product = await _repository.GetByIdAsync(id);
        if (product is null) return NotFound();

        await _repository.DeleteAsync(id);
        return NoContent();
    }

    private static ProductDto ToDto(Product p) => new(
        p.Id, p.Name, p.Description, p.Price, p.Era,
        p.Category, p.Size, p.Condition, p.ImageUrl, p.InStock
    );
}
