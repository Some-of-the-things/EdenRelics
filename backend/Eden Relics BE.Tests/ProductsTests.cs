using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class ProductsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProductsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_ReturnsSeededProducts()
    {
        var client = _factory.CreateClient();
        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        Assert.NotNull(products);
        Assert.True(products.Count >= 10, $"Expected at least 10 seeded products, got {products.Count}");
    }

    [Fact]
    public async Task GetById_ReturnsProduct()
    {
        var client = _factory.CreateClient();
        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("Bohemian Maxi Dress", product.Name);
        Assert.Equal(195m, product.Price);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/products/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByCategory_FiltersCorrectly()
    {
        var client = _factory.CreateClient();
        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products/category/70s", JsonOptions);
        Assert.NotNull(products);
        Assert.Equal(2, products.Count);
        Assert.All(products, p => Assert.Equal("70s", p.Category));
    }

    [Fact]
    public async Task GetByCategory_Empty_ReturnsEmptyList()
    {
        var client = _factory.CreateClient();
        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products/category/nonexistent", JsonOptions);
        Assert.NotNull(products);
        Assert.Empty(products);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedProduct()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-create@test.com");

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Test Dress",
            description = "A test product",
            price = 99.99m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("Test Dress", product.Name);
        Assert.Equal(99.99m, product.Price);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Hack",
            description = "Desc",
            price = 1m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-create@test.com");

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Hack",
            description = "Desc",
            price = 1m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_ModifiesProduct()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-update@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Original",
            description = "Desc",
            price = 50m,
            era = "1980s",
            category = "80s",
            size = "S",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        var updateResponse = await client.PutAsJsonAsync($"/api/products/{created!.Id}", new
        {
            name = "Updated Name",
            price = 75m
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal(75m, updated.Price);
        Assert.Equal("Desc", updated.Description);
    }

    [Fact]
    public async Task Update_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var response = await client.PutAsJsonAsync($"/api/products/{id}", new { name = "Hacked" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-update@test.com");

        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var response = await client.PutAsJsonAsync($"/api/products/{id}", new { name = "Hacked" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesProduct()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-delete@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "To Delete",
            description = "Desc",
            price = 10m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        var deleteResponse = await client.DeleteAsync($"/api/products/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var response = await client.DeleteAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-delete@test.com");

        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var response = await client.DeleteAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithSalePrice_ReturnsSalePrice()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-saleprice-create@test.com");

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Sale Dress",
            description = "On sale",
            price = 120m,
            salePrice = 89.99m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/sale.jpg",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(89.99m, product.SalePrice);
    }

    [Fact]
    public async Task Update_SetSalePrice_ReturnsSalePrice()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-saleprice-update@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "No Sale Yet",
            description = "Desc",
            price = 100m,
            era = "1980s",
            category = "80s",
            size = "S",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        var updateResponse = await client.PutAsJsonAsync($"/api/products/{created!.Id}", new
        {
            salePrice = 79.99m
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(79.99m, updated!.SalePrice);
    }

    [Fact]
    public async Task Update_ClearSalePrice_WithZero_ReturnsNull()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-saleprice-clear@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "On Sale Then Not",
            description = "Desc",
            price = 100m,
            salePrice = 60m,
            era = "1980s",
            category = "80s",
            size = "S",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(60m, created!.SalePrice);

        var updateResponse = await client.PutAsJsonAsync($"/api/products/{created.Id}", new
        {
            salePrice = 0m
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Null(updated!.SalePrice);
    }

    [Fact]
    public async Task GetAll_IncludesSalePriceInResponse()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-saleprice-getall@test.com");

        await client.PostAsJsonAsync("/api/products", new
        {
            name = "Sale In List",
            description = "Desc",
            price = 200m,
            salePrice = 150m,
            era = "1990s",
            category = "90s",
            size = "L",
            condition = "excellent",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });

        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        Assert.NotNull(products);
        var saleProduct = products.FirstOrDefault(p => p.Name == "Sale In List");
        Assert.NotNull(saleProduct);
        Assert.Equal(150m, saleProduct.SalePrice);
    }

    [Fact]
    public async Task GetById_IncludesSalePrice()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-saleprice-getbyid@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Sale Single",
            description = "Desc",
            price = 180m,
            salePrice = 140m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        var product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{created!.Id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(140m, product.SalePrice);
    }

    [Fact]
    public async Task RecordView_IncrementsViewCount()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-view-single@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "View Test",
            description = "Desc",
            price = 50m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(0, created!.ViewCount);

        var viewResponse = await client.PostAsync($"/api/products/{created.Id}/view", null);
        Assert.Equal(HttpStatusCode.NoContent, viewResponse.StatusCode);

        var product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{created.Id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(1, product.ViewCount);
    }

    [Fact]
    public async Task RecordView_MultipleViews_IncrementCorrectly()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-view-multi@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Multi View Test",
            description = "Desc",
            price = 50m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        // Same user viewing twice should only count once (unique views)
        await client.PostAsync($"/api/products/{created!.Id}/view", null);
        await client.PostAsync($"/api/products/{created.Id}/view", null);

        var product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{created.Id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(1, product.ViewCount);
    }

    [Fact]
    public async Task RecordView_DifferentUsers_CountsSeparately()
    {
        var client1 = _factory.CreateClient();
        await RegisterAdmin(client1, _factory, "admin-view-diff1@test.com");

        var createResponse = await client1.PostAsJsonAsync("/api/products", new
        {
            name = "Multi User View Test",
            description = "Desc",
            price = 50m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        await client1.PostAsync($"/api/products/{created!.Id}/view", null);

        var client2 = _factory.CreateClient();
        await RegisterAndLogin(client2, "viewer-diff2@test.com");
        await client2.PostAsync($"/api/products/{created.Id}/view", null);

        var product = await client1.GetFromJsonAsync<ProductResponse>($"/api/products/{created.Id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(2, product.ViewCount);
    }

    [Fact]
    public async Task RecordView_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/products/{Guid.Empty}/view", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadImage_AsAdmin_ReturnsImageUrl()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-upload@test.com");

        using var content = CreateImageUploadContent("test.png");
        var response = await client.PostAsync("/api/products/upload-image", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ImageUploadResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.ImageUrl));
        Assert.EndsWith(".webp", result.ImageUrl);
    }

    [Fact]
    public async Task UploadMultipleImages_AsAdmin_ReturnsDifferentUrls()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-multi-upload@test.com");

        var urls = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            using var content = CreateImageUploadContent($"photo{i}.png");
            var response = await client.PostAsync("/api/products/upload-image", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ImageUploadResponse>(JsonOptions);
            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.ImageUrl));
            urls.Add(result.ImageUrl);
        }

        Assert.Equal(3, urls.Distinct().Count());
    }

    [Fact]
    public async Task Create_WithAdditionalImages_ReturnsAllImages()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-create-multi-img@test.com");

        var additionalUrls = new List<string>
        {
            "https://example.com/img2.webp",
            "https://example.com/img3.webp",
            "https://example.com/img4.webp"
        };

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Multi Photo Dress",
            description = "A dress with multiple photos",
            price = 149.99m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "excellent",
            imageUrl = "https://example.com/img1.webp",
            additionalImageUrls = additionalUrls,
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("https://example.com/img1.webp", product.ImageUrl);
        Assert.Equal(3, product.AdditionalImageUrls.Count);
        Assert.Equal(additionalUrls, product.AdditionalImageUrls);
    }

    [Fact]
    public async Task Update_AdditionalImages_ReplacesImages()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-update-multi-img@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Update Photos Dress",
            description = "Desc",
            price = 100m,
            era = "1980s",
            category = "80s",
            size = "S",
            condition = "good",
            imageUrl = "https://example.com/original.webp",
            additionalImageUrls = new[] { "https://example.com/old1.webp" },
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Single(created!.AdditionalImageUrls);

        var newUrls = new[] { "https://example.com/new1.webp", "https://example.com/new2.webp" };
        var updateResponse = await client.PutAsJsonAsync($"/api/products/{created.Id}", new
        {
            additionalImageUrls = newUrls
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(2, updated!.AdditionalImageUrls.Count);
        Assert.Equal(newUrls, updated.AdditionalImageUrls);
    }

    [Fact]
    public async Task UploadImage_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-upload@test.com");

        using var content = CreateImageUploadContent("test.png");
        var response = await client.PostAsync("/api/products/upload-image", content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadImage_InvalidExtension_Returns400()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-upload-bad-ext@test.com");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0x00 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "document.pdf");

        var response = await client.PostAsync("/api/products/upload-image", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadAndCreate_EndToEnd_MultiplePhotos()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-e2e-photos@test.com");

        // Upload 3 images
        var uploadedUrls = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            using var content = CreateImageUploadContent($"dress{i}.jpg");
            var uploadResponse = await client.PostAsync("/api/products/upload-image", content);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

            var result = await uploadResponse.Content.ReadFromJsonAsync<ImageUploadResponse>(JsonOptions);
            uploadedUrls.Add(result!.ImageUrl);
        }

        // Create product using first as primary, rest as additional
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "E2E Photo Dress",
            description = "End to end test",
            price = 175m,
            era = "1970s",
            category = "70s",
            size = "M",
            condition = "excellent",
            imageUrl = uploadedUrls[0],
            additionalImageUrls = uploadedUrls.Skip(1).ToList(),
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(uploadedUrls[0], product.ImageUrl);
        Assert.Equal(2, product.AdditionalImageUrls.Count);
        Assert.Equal(uploadedUrls[1], product.AdditionalImageUrls[0]);
        Assert.Equal(uploadedUrls[2], product.AdditionalImageUrls[1]);

        // Verify via GET
        var fetched = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}", JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(uploadedUrls[0], fetched.ImageUrl);
        Assert.Equal(2, fetched.AdditionalImageUrls.Count);
    }

    [Fact]
    public async Task Create_WithMoreThan6AdditionalImages_Returns400()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-too-many-imgs@test.com");

        var urls = Enumerable.Range(1, 7).Select(i => $"https://example.com/img{i}.webp").ToList();
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Too Many Photos",
            description = "Desc",
            price = 100m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/primary.webp",
            additionalImageUrls = urls,
            inStock = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithMoreThan6AdditionalImages_Returns400()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-too-many-update@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Will Overflow",
            description = "Desc",
            price = 100m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/primary.webp",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        var urls = Enumerable.Range(1, 7).Select(i => $"https://example.com/img{i}.webp").ToList();
        var response = await client.PutAsJsonAsync($"/api/products/{created!.Id}", new
        {
            additionalImageUrls = urls
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static MultipartFormDataContent CreateImageUploadContent(string fileName)
    {
        using var image = new Image<Rgba32>(100, 100);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        var bytes = ms.ToArray();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private record ImageUploadResponse(string ImageUrl);
}
