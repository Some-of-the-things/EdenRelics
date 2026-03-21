using System.Security.Claims;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IRepository<Product> _repository;
    private readonly IRepository<Favourite> _favouriteRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<ProductView> _viewRepository;
    private readonly IWebHostEnvironment _env;
    private readonly ImageStorageService _storage;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IRepository<Product> repository,
        IRepository<Favourite> favouriteRepository,
        IRepository<User> userRepository,
        IRepository<ProductView> viewRepository,
        IWebHostEnvironment env,
        ImageStorageService storage,
        IEmailService emailService,
        ILogger<ProductsController> logger)
    {
        _repository = repository;
        _favouriteRepository = favouriteRepository;
        _userRepository = userRepository;
        _viewRepository = viewRepository;
        _env = env;
        _storage = storage;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        IEnumerable<Product> products = await _repository.GetAllAsync();
        if (User.IsInRole("Admin"))
        {
            return Ok(products.Select(ToAdminDto));
        }
        return Ok(products.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetById(Guid id)
    {
        Product? product = await _repository.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }
        if (User.IsInRole("Admin"))
        {
            return Ok(ToAdminDto(product));
        }
        return Ok(ToDto(product));
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult> GetByCategory(string category)
    {
        IEnumerable<Product> products = await _repository.FindAsync(p => p.Category == category);
        if (User.IsInRole("Admin"))
        {
            return Ok(products.Select(ToAdminDto));
        }
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
            CostPrice = dto.CostPrice,
            Supplier = dto.Supplier,
            Era = dto.Era,
            Category = dto.Category,
            Size = dto.Size,
            Condition = dto.Condition,
            ImageUrl = dto.ImageUrl,
            AdditionalImageUrls = dto.AdditionalImageUrls ?? [],
            InStock = dto.InStock,
            SalePrice = dto.SalePrice
        };

        await _repository.AddAsync(product);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, ToAdminDto(product));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpdateProductDto dto)
    {
        Product? product = await _repository.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        if (dto.Name is not null) { product.Name = dto.Name; }
        if (dto.Description is not null) { product.Description = dto.Description; }
        if (dto.Price.HasValue) { product.Price = dto.Price.Value; }
        if (dto.CostPrice.HasValue) { product.CostPrice = dto.CostPrice.Value; }
        if (dto.Supplier is not null) { product.Supplier = dto.Supplier; }
        if (dto.Era is not null) { product.Era = dto.Era; }
        if (dto.Category is not null) { product.Category = dto.Category; }
        if (dto.Size is not null) { product.Size = dto.Size; }
        if (dto.Condition is not null) { product.Condition = dto.Condition; }
        if (dto.ImageUrl is not null) { product.ImageUrl = dto.ImageUrl; }
        if (dto.AdditionalImageUrls is not null) { product.AdditionalImageUrls = dto.AdditionalImageUrls; }
        if (dto.InStock.HasValue) { product.InStock = dto.InStock.Value; }
        if (dto.SalePrice.HasValue)
        {
            decimal? oldSalePrice = product.SalePrice;
            product.SalePrice = dto.SalePrice.Value == 0 ? null : dto.SalePrice.Value;

            // Notify favourited users if product was just put on sale
            if (product.SalePrice.HasValue && oldSalePrice != product.SalePrice)
            {
                _ = NotifySaleFavouritesAsync(product);
            }
        }

        await _repository.UpdateAsync(product);
        return Ok(ToAdminDto(product));
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

        try
        {
            string imageUrl = await ImageUploadHelper.ProcessAndUploadAsync(
                file.OpenReadStream(), _storage, _env, Request, "products");
            return Ok(new { imageUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload product image (file: {FileName}, size: {Size})", file.FileName, file.Length);
            return StatusCode(500, new { error = "Image upload failed. Please try again." });
        }
    }

    [HttpPost("{id:guid}/view")]
    public async Task<IActionResult> RecordView(Guid id)
    {
        Product? product = await _repository.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = Guid.TryParse(userIdStr, out Guid parsed) ? parsed : null;
        string? ip = Request.Headers["Fly-Client-IP"].FirstOrDefault()
            ?? Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? HttpContext.Connection.RemoteIpAddress?.ToString();

        // Check for existing view by this user or IP
        IEnumerable<ProductView> existingViews = await _viewRepository.FindAsync(v =>
            v.ProductId == id &&
            ((userId != null && v.UserId == userId) ||
             (userId == null && v.IpAddress == ip)));

        if (existingViews.Any())
        {
            return NoContent();
        }

        await _viewRepository.AddAsync(new ProductView
        {
            ProductId = id,
            UserId = userId,
            IpAddress = ip
        });

        product.ViewCount++;
        await _repository.UpdateAsync(product);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        Product? product = await _repository.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(id);
        return NoContent();
    }

    private static ProductDto ToDto(Product p) => new(
        p.Id, p.Name, p.Description, p.Price, p.SalePrice, p.Era,
        p.Category, p.Size, p.Condition, p.ImageUrl, p.AdditionalImageUrls, p.InStock
    );

    private static ProductAdminDto ToAdminDto(Product p) => new(
        p.Id, p.Name, p.Description, p.Price, p.SalePrice, p.CostPrice, p.Supplier, p.Era,
        p.Category, p.Size, p.Condition, p.ImageUrl, p.AdditionalImageUrls, p.InStock, p.ViewCount
    );

    private async Task NotifySaleFavouritesAsync(Product product)
    {
        try
        {
            IEnumerable<Favourite> favourites = await _favouriteRepository.FindAsync(f => f.ProductId == product.Id);
            foreach (Favourite fav in favourites)
            {
                User? user = await _userRepository.GetByIdAsync(fav.UserId);
                if (user is null)
                {
                    continue;
                }
                _ = _emailService.SendSaleNotificationAsync(user.Email, user.FirstName, product.Name, product.Price, product.SalePrice!.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send sale notifications for product {ProductId}", product.Id);
        }
    }
}
