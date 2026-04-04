using System.Security.Claims;
using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
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
    private readonly GeoIpService _geoIp;
    private readonly IConfiguration _config;
    private readonly ILogger<ProductsController> _logger;
    private readonly TranslationService _translation;

    public ProductsController(
        IRepository<Product> repository,
        IRepository<Favourite> favouriteRepository,
        IRepository<User> userRepository,
        IRepository<ProductView> viewRepository,
        IWebHostEnvironment env,
        ImageStorageService storage,
        IEmailService emailService,
        GeoIpService geoIp,
        IConfiguration config,
        ILogger<ProductsController> logger,
        TranslationService translation)
    {
        _repository = repository;
        _favouriteRepository = favouriteRepository;
        _userRepository = userRepository;
        _viewRepository = viewRepository;
        _env = env;
        _storage = storage;
        _emailService = emailService;
        _geoIp = geoIp;
        _config = config;
        _logger = logger;
        _translation = translation;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] string? locale = null)
    {
        IEnumerable<Product> products = await _repository.GetAllAsync();
        if (User.IsInRole("Admin"))
        {
            return Ok(products.Select(ToAdminDto));
        }
        return Ok(products.Select(p => ToDto(p, locale)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetById(Guid id, [FromQuery] string? locale = null)
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
        return Ok(ToDto(product, locale));
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult> GetByCategory(string category, [FromQuery] string? locale = null)
    {
        IEnumerable<Product> products = await _repository.FindAsync(p => p.Category == category);
        if (User.IsInRole("Admin"))
        {
            return Ok(products.Select(ToAdminDto));
        }
        return Ok(products.Select(p => ToDto(p, locale)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create(CreateProductDto dto)
    {
        if (dto.AdditionalImageUrls is { Count: > 10 })
        {
            return BadRequest(new { error = "A product can have at most 10 additional images." });
        }

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
            VideoUrls = dto.VideoUrls ?? [],
            InStock = dto.InStock,
            SalePrice = dto.SalePrice
        };

        await _repository.AddAsync(product);

        // Translate name and description in the background
        _ = TranslateProductAsync(product);

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
        if (dto.AdditionalImageUrls is not null)
        {
            if (dto.AdditionalImageUrls.Count > 10)
            {
                return BadRequest(new { error = "A product can have at most 10 additional images." });
            }
            product.AdditionalImageUrls = dto.AdditionalImageUrls;
        }
        if (dto.VideoUrls is not null) { product.VideoUrls = dto.VideoUrls; }
        if (dto.InStock.HasValue) { product.InStock = dto.InStock.Value; }
        bool shouldNotifySale = false;
        if (dto.SalePrice.HasValue)
        {
            decimal? oldSalePrice = product.SalePrice;
            product.SalePrice = dto.SalePrice.Value == 0 ? null : dto.SalePrice.Value;
            shouldNotifySale = product.SalePrice.HasValue && oldSalePrice != product.SalePrice;
        }

        await _repository.UpdateAsync(product);

        // Translate updated name/description in the background
        if (dto.Name is not null || dto.Description is not null)
        {
            _ = TranslateProductAsync(product);
        }

        // Notify after update completes to avoid concurrent DbContext access
        if (shouldNotifySale)
        {
            _ = NotifySaleFavouritesAsync(product);
        }

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

    [HttpPost("upload-video")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> UploadVideo([FromForm] IFormFile file)
    {
        string[] allowedExtensions = [".mp4", ".mov", ".webm", ".avi"];
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Only video files (mp4, mov, webm, avi) are allowed." });
        }

        if (file.Length > 100 * 1024 * 1024)
        {
            return BadRequest(new { error = "File size must be under 100MB." });
        }

        try
        {
            string contentType = extension switch
            {
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".webm" => "video/webm",
                ".avi" => "video/x-msvideo",
                _ => "video/mp4",
            };

            string fileName = $"products/{Guid.NewGuid()}{extension}";

            if (_storage.IsConfigured)
            {
                using var stream = file.OpenReadStream();
                string videoUrl = await _storage.UploadAsync(stream, fileName, contentType);
                return Ok(new { videoUrl });
            }

            // Fallback to local filesystem
            string uploadsDir = Path.Combine(
                _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"),
                "uploads", "products");
            Directory.CreateDirectory(uploadsDir);

            string localFileName = Path.GetFileName(fileName);
            string filePath = Path.Combine(uploadsDir, localFileName);
            await using var fs = new FileStream(filePath, FileMode.Create);
            await file.OpenReadStream().CopyToAsync(fs);

            string videoUrlLocal = $"{Request.Scheme}://{Request.Host}/uploads/products/{localFileName}";
            return Ok(new { videoUrl = videoUrlLocal });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload product video (file: {FileName}, size: {Size})", file.FileName, file.Length);
            return StatusCode(500, new { error = "Video upload failed. Please try again." });
        }
    }

    [HttpPost("{id:guid}/view")]
    public async Task<IActionResult> RecordView(Guid id, [FromBody] RecordViewDto? dto = null)
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

        string? channel = DeriveChannel(dto?.Referrer, dto?.UtmSource, dto?.UtmMedium);
        string? country = await _geoIp.GetCountryAsync(ip);
        string? userAgent = Request.Headers.UserAgent.FirstOrDefault();
        (string deviceType, string os) = ParseUserAgent(userAgent);

        await _viewRepository.AddAsync(new ProductView
        {
            ProductId = id,
            UserId = userId,
            IpAddress = ip,
            ReferrerUrl = dto?.Referrer?[..Math.Min(dto.Referrer.Length, 2000)],
            UtmSource = dto?.UtmSource,
            UtmMedium = dto?.UtmMedium,
            UtmCampaign = dto?.UtmCampaign,
            Channel = channel,
            Country = country,
            UserAgent = userAgent?[..Math.Min(userAgent.Length, 500)],
            DeviceType = deviceType,
            OperatingSystem = os,
            ScreenResolution = dto?.ScreenResolution,
        });

        product.ViewCount++;
        await _repository.UpdateAsync(product);
        return NoContent();
    }

    public record RecordViewDto(string? Referrer = null, string? UtmSource = null, string? UtmMedium = null, string? UtmCampaign = null, string? ScreenResolution = null);

    [HttpGet("{id:guid}/views")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetViewAnalytics(Guid id)
    {
        Product? product = await _repository.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        IEnumerable<ProductView> views = await _viewRepository.FindAsync(v => v.ProductId == id);
        List<ProductView> viewList = views.OrderByDescending(v => v.CreatedAtUtc).ToList();

        var individualViews = viewList.Select(v => new
        {
            v.CreatedAtUtc,
            channel = v.Channel ?? "direct",
            v.Country,
            referrer = !string.IsNullOrEmpty(v.ReferrerUrl) ? ExtractDomain(v.ReferrerUrl) : null,
            v.DeviceType,
            v.OperatingSystem,
            v.ScreenResolution,
            v.UtmSource,
            v.UtmMedium,
            v.UtmCampaign,
        }).ToList();

        var byChannel = viewList
            .GroupBy(v => v.Channel ?? "direct")
            .Select(g => new { channel = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        var byCountry = viewList
            .Where(v => !string.IsNullOrEmpty(v.Country))
            .GroupBy(v => v.Country!)
            .Select(g => new { country = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        var topReferrers = viewList
            .Where(v => !string.IsNullOrEmpty(v.ReferrerUrl))
            .GroupBy(v => ExtractDomain(v.ReferrerUrl!))
            .Select(g => new { referrer = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToList();

        var viewsByDate = viewList
            .Where(v => v.CreatedAtUtc >= DateTime.UtcNow.AddDays(-30))
            .GroupBy(v => v.CreatedAtUtc.ToString("yyyy-MM-dd"))
            .Select(g => new { date = g.Key, count = g.Count() })
            .OrderBy(x => x.date)
            .ToList();

        var byDevice = viewList
            .Where(v => !string.IsNullOrEmpty(v.DeviceType))
            .GroupBy(v => v.DeviceType!)
            .Select(g => new { device = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        var byOs = viewList
            .Where(v => !string.IsNullOrEmpty(v.OperatingSystem))
            .GroupBy(v => v.OperatingSystem!)
            .Select(g => new { os = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        return Ok(new
        {
            totalViews = Math.Max(product.ViewCount, viewList.Count),
            trackedViews = viewList.Count,
            views = individualViews,
            byChannel,
            byCountry,
            topReferrers,
            viewsByDate,
            byDevice,
            byOs,
        });
    }

    private static (string deviceType, string os) ParseUserAgent(string? ua)
    {
        if (string.IsNullOrEmpty(ua))
        {
            return ("Unknown", "Unknown");
        }

        string uaLower = ua.ToLowerInvariant();

        // Device type
        string device;
        if (uaLower.Contains("mobile") || uaLower.Contains("android") && !uaLower.Contains("tablet"))
        {
            device = "Mobile";
        }
        else if (uaLower.Contains("tablet") || uaLower.Contains("ipad"))
        {
            device = "Tablet";
        }
        else
        {
            device = "Desktop";
        }

        // OS
        string os;
        if (uaLower.Contains("windows"))
        {
            os = "Windows";
        }
        else if (uaLower.Contains("macintosh") || uaLower.Contains("mac os"))
        {
            os = "macOS";
        }
        else if (uaLower.Contains("iphone") || uaLower.Contains("ipad"))
        {
            os = "iOS";
        }
        else if (uaLower.Contains("android"))
        {
            os = "Android";
        }
        else if (uaLower.Contains("linux"))
        {
            os = "Linux";
        }
        else if (uaLower.Contains("cros"))
        {
            os = "ChromeOS";
        }
        else
        {
            os = "Other";
        }

        return (device, os);
    }

    private static string DeriveChannel(string? referrer, string? utmSource, string? utmMedium)
    {
        if (!string.IsNullOrEmpty(utmMedium))
        {
            string medium = utmMedium.ToLowerInvariant();
            if (medium is "cpc" or "ppc" or "paid")
            {
                return "paid";
            }
            if (medium == "email")
            {
                return "email";
            }
            if (medium == "social")
            {
                return "social";
            }
        }

        if (!string.IsNullOrEmpty(referrer))
        {
            string domain = ExtractDomain(referrer).ToLowerInvariant();
            string[] socialDomains = ["facebook.com", "instagram.com", "twitter.com", "x.com", "tiktok.com", "pinterest.com", "linkedin.com"];
            if (socialDomains.Any(s => domain.Contains(s)))
            {
                return "social";
            }

            string[] searchDomains = ["google.", "bing.com", "yahoo.com", "duckduckgo.com", "baidu.com"];
            if (searchDomains.Any(s => domain.Contains(s)))
            {
                return "search";
            }

            return "referral";
        }

        if (!string.IsNullOrEmpty(utmSource))
        {
            return "referral";
        }

        return "direct";
    }

    private static string ExtractDomain(string url)
    {
        try
        {
            return new Uri(url).Host;
        }
        catch
        {
            return url;
        }
    }

    [HttpPost("analyse-image")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> AnalyseImage([FromBody] AnalyseImageRequest request)
    {
        string? apiKey = _config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(503, new { error = "Anthropic API key not configured." });
        }

        try
        {
            var client = new AnthropicClient(apiKey);

            // Fetch recent product descriptions as style examples
            IEnumerable<Product> recentProducts = await _repository.GetAllAsync();
            List<Product> examples = recentProducts
                .Where(p => !string.IsNullOrWhiteSpace(p.Description) && p.Description.Length > 20)
                .OrderByDescending(p => p.CreatedAtUtc)
                .Take(5)
                .ToList();

            string styleExamples = "";
            if (examples.Count > 0)
            {
                styleExamples = "\n\nHere are existing product listings from this shop. Match their writing style, tone, length, and formatting exactly:\n\n";
                foreach (Product ex in examples)
                {
                    styleExamples += $"Name: {ex.Name}\nDescription: {ex.Description}\n\n";
                }
            }

            // Fetch the image and convert to base64
            byte[] imageBytes;
            if (request.ImageUrl.Contains("/uploads/"))
            {
                // Local file — read directly from disk
                string relativePath = request.ImageUrl[(request.ImageUrl.IndexOf("/uploads/") + 1)..];
                string filePath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), relativePath);
                imageBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            }
            else
            {
                using HttpClient http = new();
                imageBytes = await http.GetByteArrayAsync(request.ImageUrl);
            }
            string base64 = Convert.ToBase64String(imageBytes);
            string mediaType = request.ImageUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/webp";

            var messages = new List<Message>
            {
                new()
                {
                    Role = RoleType.User,
                    Content =
                    [
                        new ImageContent
                        {
                            Source = new ImageSource
                            {
                                MediaType = mediaType,
                                Data = base64,
                            }
                        },
                        new TextContent
                        {
                            Text = $"""
                                You are a vintage clothing expert cataloguing items for an online shop called Eden Relics.
                                Analyse this clothing image and return a JSON object with these fields:
                                - "name": a short, appealing product name (e.g. "Silk Slip Dress", "Power Shoulder Blazer")
                                - "description": a rich product description highlighting era, fabric, details, and styling. Use the same HTML formatting as the examples below if provided.
                                - "era": the decade it's from as a string like "1970s", "1980s", "1990s", "2000s", or "2020s"
                                - "category": one of "70s", "80s", "90s", "y2k", or "modern" (matching the era)
                                - "size": your best guess at UK size as a string (e.g. "8", "10", "12", "14", "16"), or "10" if unclear
                                - "condition": one of "mint", "excellent", "very good", "good", or "fair"
                                - "suggestedPrice": a suggested retail price in GBP as a number (no currency symbol)
                                {styleExamples}
                                Return ONLY valid JSON, no markdown fences, no explanation.
                                """
                        }
                    ]
                }
            };

            var parameters = new MessageParameters
            {
                Messages = messages,
                MaxTokens = 512,
                Model = AnthropicModels.Claude45Haiku,
                Stream = false,
                Temperature = 0.3m,
            };

            var result = await client.Messages.GetClaudeMessageAsync(parameters);
            string responseText = result.Message.ToString().Trim();

            // Extract JSON object from response regardless of surrounding text
            int jsonStart = responseText.IndexOf('{');
            int jsonEnd = responseText.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                _logger.LogWarning("Analyse-image response contained no JSON: {Response}", responseText);
                return BadRequest(new { error = "The image doesn't appear to be a clothing item. Please upload a photo of a garment." });
            }
            string json = responseText[jsonStart..(jsonEnd + 1)];

            JsonDocument parsed = JsonDocument.Parse(json);
            return Ok(parsed.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyse product image");
            return StatusCode(500, new { error = "Image analysis failed. Please try again." });
        }
    }

    public record AnalyseImageRequest(string ImageUrl);

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

    private static ProductDto ToDto(Product p, string? locale = null)
    {
        string name = p.Name;
        string description = p.Description;
        if (locale is not null && locale != "en")
        {
            if (p.NameTranslations.TryGetValue(locale, out string? tn))
            {
                name = tn;
            }
            if (p.DescriptionTranslations.TryGetValue(locale, out string? td))
            {
                description = td;
            }
        }
        return new(p.Id, name, description, p.Price, p.SalePrice, p.Era,
            p.Category, p.Size, p.Condition, p.ImageUrl, p.AdditionalImageUrls, p.VideoUrls, p.InStock);
    }

    private static ProductAdminDto ToAdminDto(Product p) => new(
        p.Id, p.Name, p.Description, p.Price, p.SalePrice, p.CostPrice, p.Supplier, p.Era,
        p.Category, p.Size, p.Condition, p.ImageUrl, p.AdditionalImageUrls, p.VideoUrls, p.InStock, p.ViewCount
    );

    private async Task TranslateProductAsync(Product product)
    {
        try
        {
            Dictionary<string, string> nameTranslations = await _translation.TranslateTextAsync(product.Name);
            Dictionary<string, string> descTranslations = await _translation.TranslateTextAsync(product.Description);

            product.NameTranslations = nameTranslations;
            product.DescriptionTranslations = descTranslations;
            await _repository.UpdateAsync(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate product {ProductId}", product.Id);
        }
    }

    private async Task NotifySaleFavouritesAsync(Product product)
    {
        try
        {
            IEnumerable<Favourite> favourites = await _favouriteRepository.FindAsync(
                f => f.ProductId == product.Id && f.NotifyOnSale);
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
