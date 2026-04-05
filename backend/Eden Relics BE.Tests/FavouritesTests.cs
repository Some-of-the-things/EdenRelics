using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class FavouritesTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly Guid SeededProductId1 = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
    private static readonly Guid SeededProductId2 = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000002");

    public FavouritesTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetFavourites_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/favourites");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFavourites_Empty_ReturnsEmptyList()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "fav-empty@test.com");

        List<FavouriteDto>? favourites = await client.GetFromJsonAsync<List<FavouriteDto>>("/api/favourites", JsonOptions);
        Assert.NotNull(favourites);
        Assert.Empty(favourites);
    }

    [Fact]
    public async Task AddFavourite_AuthenticatedUser_Returns201()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "fav-add@test.com");

        HttpResponseMessage response = await client.PostAsync($"/api/favourites/{SeededProductId1}", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AddFavourite_Duplicate_Returns200()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "fav-dup@test.com");

        await client.PostAsync($"/api/favourites/{SeededProductId1}", null);
        HttpResponseMessage response = await client.PostAsync($"/api/favourites/{SeededProductId1}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AddFavourite_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync($"/api/favourites/{SeededProductId1}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RemoveFavourite_Authenticated_Returns204()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "fav-remove@test.com");

        await client.PostAsync($"/api/favourites/{SeededProductId1}", null);
        HttpResponseMessage response = await client.DeleteAsync($"/api/favourites/{SeededProductId1}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RemoveFavourite_NotFound_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "fav-notfound@test.com");

        HttpResponseMessage response = await client.DeleteAsync($"/api/favourites/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveFavourite_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.DeleteAsync($"/api/favourites/{SeededProductId1}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFavourites_ReturnsAddedProducts()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "fav-list@test.com");

        await client.PostAsync($"/api/favourites/{SeededProductId1}", null);
        await client.PostAsync($"/api/favourites/{SeededProductId2}", null);

        List<FavouriteDto>? favourites = await client.GetFromJsonAsync<List<FavouriteDto>>("/api/favourites", JsonOptions);
        Assert.NotNull(favourites);
        Assert.Equal(2, favourites.Count);
        Assert.Contains(favourites, f => f.ProductId == SeededProductId1);
        Assert.Contains(favourites, f => f.ProductId == SeededProductId2);
    }

    private record FavouriteDto(Guid ProductId, bool NotifyOnSale);
}
