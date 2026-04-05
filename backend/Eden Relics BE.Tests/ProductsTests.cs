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
        HttpClient client = _factory.CreateClient();
        List<ProductResponse>? products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        Assert.NotNull(products);
        Assert.True(products.Count >= 10, $"Expected at least 10 seeded products, got {products.Count}");
    }

    [Fact]
    public async Task GetById_ReturnsProduct()
    {
        HttpClient client = _factory.CreateClient();
        Guid id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        ProductResponse? product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("Bohemian Maxi Dress", product.Name);
        Assert.Equal(195m, product.Price);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/products/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByCategory_FiltersCorrectly()
    {
        HttpClient client = _factory.CreateClient();
        List<ProductResponse>? products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products/category/70s", JsonOptions);
        Assert.NotNull(products);
        Assert.Equal(2, products.Count);
        Assert.All(products, p => Assert.Equal("70s", p.Category));
    }

    [Fact]
    public async Task GetByCategory_Empty_ReturnsEmptyList()
    {
        HttpClient client = _factory.CreateClient();
        List<ProductResponse>? products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products/category/nonexistent", JsonOptions);
        Assert.NotNull(products);
        Assert.Empty(products);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedProduct()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-create@test.com");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("Test Dress", product.Name);
        Assert.Equal(99.99m, product.Price);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
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
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-create@test.com");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
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
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-update@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/products/{created!.Id}", new
        {
            name = "Updated Name",
            price = 75m
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        ProductResponse? updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal(75m, updated.Price);
        Assert.Equal("Desc", updated.Description);
    }

    [Fact]
    public async Task Update_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        Guid id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/products/{id}", new { name = "Hacked" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-update@test.com");

        Guid id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/products/{id}", new { name = "Hacked" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesProduct()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-delete@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/products/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage getResponse = await client.GetAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        Guid id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        HttpResponseMessage response = await client.DeleteAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-delete@test.com");

        Guid id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        HttpResponseMessage response = await client.DeleteAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithSalePrice_ReturnsSalePrice()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-saleprice-create@test.com");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(89.99m, product.SalePrice);
    }

    [Fact]
    public async Task Update_SetSalePrice_ReturnsSalePrice()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-saleprice-update@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/products/{created!.Id}", new
        {
            salePrice = 79.99m
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        ProductResponse? updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(79.99m, updated!.SalePrice);
    }

    [Fact]
    public async Task Update_ClearSalePrice_WithZero_ReturnsNull()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-saleprice-clear@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(60m, created!.SalePrice);

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/products/{created.Id}", new
        {
            salePrice = 0m
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        ProductResponse? updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Null(updated!.SalePrice);
    }

    [Fact]
    public async Task GetAll_IncludesSalePriceInResponse()
    {
        HttpClient client = _factory.CreateClient();
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

        List<ProductResponse>? products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        Assert.NotNull(products);
        ProductResponse? saleProduct = products.FirstOrDefault(p => p.Name == "Sale In List");
        Assert.NotNull(saleProduct);
        Assert.Equal(150m, saleProduct.SalePrice);
    }

    [Fact]
    public async Task GetById_IncludesSalePrice()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-saleprice-getbyid@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        ProductResponse? product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{created!.Id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(140m, product.SalePrice);
    }

    [Fact]
    public async Task RecordView_IncrementsViewCount()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-view-single@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(0, created!.ViewCount);

        HttpResponseMessage viewResponse = await client.PostAsync($"/api/products/{created.Id}/view", null);
        Assert.Equal(HttpStatusCode.NoContent, viewResponse.StatusCode);

        ProductResponse? product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{created.Id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(1, product.ViewCount);
    }

    [Fact]
    public async Task RecordView_MultipleViews_IncrementCorrectly()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-view-multi@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        // Same user viewing twice should only count once (unique views)
        await client.PostAsync($"/api/products/{created!.Id}/view", null);
        await client.PostAsync($"/api/products/{created.Id}/view", null);

        ProductResponse? product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{created.Id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(1, product.ViewCount);
    }

    [Fact]
    public async Task RecordView_DifferentUsers_CountsSeparately()
    {
        HttpClient client1 = _factory.CreateClient();
        await RegisterAdmin(client1, _factory, "admin-view-diff1@test.com");

        HttpResponseMessage createResponse = await client1.PostAsJsonAsync("/api/products", new
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
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        await client1.PostAsync($"/api/products/{created!.Id}/view", null);

        HttpClient client2 = _factory.CreateClient();
        await RegisterAndLogin(client2, "viewer-diff2@test.com");
        await client2.PostAsync($"/api/products/{created.Id}/view", null);

        ProductResponse? product = await client1.GetFromJsonAsync<ProductResponse>($"/api/products/{created.Id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(2, product.ViewCount);
    }

    [Fact]
    public async Task RecordView_NonExistent_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync($"/api/products/{Guid.Empty}/view", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadImage_AsAdmin_ReturnsImageUrl()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-upload@test.com");

        using MultipartFormDataContent content = CreateImageUploadContent("test.png");
        HttpResponseMessage response = await client.PostAsync("/api/products/upload-image", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        ImageUploadResponse? result = await response.Content.ReadFromJsonAsync<ImageUploadResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.ImageUrl));
        Assert.EndsWith(".webp", result.ImageUrl);
    }

    [Fact]
    public async Task UploadMultipleImages_AsAdmin_ReturnsDifferentUrls()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-multi-upload@test.com");

        List<string> urls = new();
        for (int i = 0; i < 3; i++)
        {
            using MultipartFormDataContent content = CreateImageUploadContent($"photo{i}.png");
            HttpResponseMessage response = await client.PostAsync("/api/products/upload-image", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            ImageUploadResponse? result = await response.Content.ReadFromJsonAsync<ImageUploadResponse>(JsonOptions);
            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.ImageUrl));
            urls.Add(result.ImageUrl);
        }

        Assert.Equal(3, urls.Distinct().Count());
    }

    [Fact]
    public async Task Create_WithAdditionalImages_ReturnsAllImages()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-create-multi-img@test.com");

        List<string> additionalUrls =
        [
            "https://example.com/img2.webp",
            "https://example.com/img3.webp",
            "https://example.com/img4.webp"
        ];

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
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

        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("https://example.com/img1.webp", product.ImageUrl);
        Assert.Equal(3, product.AdditionalImageUrls.Count);
        Assert.Equal(additionalUrls, product.AdditionalImageUrls);
    }

    [Fact]
    public async Task Update_AdditionalImages_ReplacesImages()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-update-multi-img@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Single(created!.AdditionalImageUrls);

        string[] newUrls = ["https://example.com/new1.webp", "https://example.com/new2.webp"];
        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/products/{created.Id}", new
        {
            additionalImageUrls = newUrls
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        ProductResponse? updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(2, updated!.AdditionalImageUrls.Count);
        Assert.Equal(newUrls, updated.AdditionalImageUrls);
    }

    [Fact]
    public async Task UploadImage_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-upload@test.com");

        using MultipartFormDataContent content = CreateImageUploadContent("test.png");
        HttpResponseMessage response = await client.PostAsync("/api/products/upload-image", content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadImage_InvalidExtension_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-upload-bad-ext@test.com");

        using MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(new byte[] { 0x00 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "document.pdf");

        HttpResponseMessage response = await client.PostAsync("/api/products/upload-image", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadAndCreate_EndToEnd_MultiplePhotos()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-e2e-photos@test.com");

        // Upload 3 images
        List<string> uploadedUrls = new();
        for (int i = 0; i < 3; i++)
        {
            using MultipartFormDataContent content = CreateImageUploadContent($"dress{i}.jpg");
            HttpResponseMessage uploadResponse = await client.PostAsync("/api/products/upload-image", content);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

            ImageUploadResponse? result = await uploadResponse.Content.ReadFromJsonAsync<ImageUploadResponse>(JsonOptions);
            uploadedUrls.Add(result!.ImageUrl);
        }

        // Create product using first as primary, rest as additional
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
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

        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(uploadedUrls[0], product.ImageUrl);
        Assert.Equal(2, product.AdditionalImageUrls.Count);
        Assert.Equal(uploadedUrls[1], product.AdditionalImageUrls[0]);
        Assert.Equal(uploadedUrls[2], product.AdditionalImageUrls[1]);

        // Verify via GET
        ProductResponse? fetched = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}", JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(uploadedUrls[0], fetched.ImageUrl);
        Assert.Equal(2, fetched.AdditionalImageUrls.Count);
    }

    [Fact]
    public async Task Create_WithMoreThan10AdditionalImages_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-too-many-imgs@test.com");

        List<string> urls = Enumerable.Range(1, 11).Select(i => $"https://example.com/img{i}.webp").ToList();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
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
    public async Task Create_With10AdditionalImages_Succeeds()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-10-imgs@test.com");

        List<string> urls = Enumerable.Range(1, 10).Select(i => $"https://example.com/img{i}.webp").ToList();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Max Photos",
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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(10, product!.AdditionalImageUrls.Count);
    }

    [Fact]
    public async Task Update_WithMoreThan10AdditionalImages_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-too-many-update@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
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
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        List<string> urls = Enumerable.Range(1, 11).Select(i => $"https://example.com/img{i}.webp").ToList();
        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/products/{created!.Id}", new
        {
            additionalImageUrls = urls
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithVideoUrls_ReturnsVideoUrls()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-video-create@test.com");

        string[] videoUrls = ["https://example.com/vid1.mp4", "https://example.com/vid2.mp4"];
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Video Dress",
            description = "Desc",
            price = 100m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.webp",
            videoUrls,
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal(2, product.VideoUrls.Count);
        Assert.Equal(videoUrls, product.VideoUrls);
    }

    [Fact]
    public async Task Update_VideoUrls_ReplacesVideos()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-video-update@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Video Update Dress",
            description = "Desc",
            price = 100m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.webp",
            videoUrls = new[] { "https://example.com/old.mp4" },
            inStock = true
        });
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        string[] newUrls = ["https://example.com/new1.mp4", "https://example.com/new2.webm"];
        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/products/{created!.Id}", new
        {
            videoUrls = newUrls
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        ProductResponse? updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(2, updated!.VideoUrls.Count);
        Assert.Equal(newUrls, updated.VideoUrls);
    }

    [Fact]
    public async Task UploadVideo_AsAdmin_ReturnsVideoUrl()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-upload-video@test.com");

        using MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(new byte[1024]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(fileContent, "file", "test.mp4");

        HttpResponseMessage response = await client.PostAsync("/api/products/upload-video", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        VideoUploadResponse? result = await response.Content.ReadFromJsonAsync<VideoUploadResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.VideoUrl));
    }

    [Fact]
    public async Task UploadVideo_InvalidExtension_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-upload-video-bad@test.com");

        using MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(new byte[100]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "document.pdf");

        HttpResponseMessage response = await client.PostAsync("/api/products/upload-video", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadVideo_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-upload-video@test.com");

        using MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(new byte[100]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(fileContent, "file", "test.mp4");

        HttpResponseMessage response = await client.PostAsync("/api/products/upload-video", content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static MultipartFormDataContent CreateImageUploadContent(string fileName)
    {
        using Image<Rgba32> image = new(100, 100);
        using MemoryStream ms = new();
        image.SaveAsPng(ms);
        byte[] bytes = ms.ToArray();

        MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private record ImageUploadResponse(string ImageUrl);
    private record VideoUploadResponse(string VideoUrl);
}
