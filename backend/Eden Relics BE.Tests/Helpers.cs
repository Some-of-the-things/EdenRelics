using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Eden_Relics_BE.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Eden_Relics_BE.Tests;

public static class Helpers
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<(string Token, AuthResponse Auth)> RegisterAndLogin(HttpClient client, string email = "test@example.com")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "TestPass123!",
            firstName = "Test",
            lastName = "User"
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return (auth.Token, auth);
    }

    public static async Task<(string Token, AuthResponse Auth)> RegisterAdmin(HttpClient client, ApiFactory factory, string email = "admin@example.com")
    {
        // Register as normal user
        var (_, auth) = await RegisterAndLogin(client, email);

        // Promote to Admin in the database
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        var user = await db.Users.FindAsync(auth.User.Id);
        user!.Role = "Admin";
        await db.SaveChangesAsync();

        // Re-login to get a token with the Admin role claim
        client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "TestPass123!"
        });
        loginResponse.EnsureSuccessStatusCode();
        var adminAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.Token);
        return (adminAuth.Token, adminAuth);
    }

    public record AuthResponse(string Token, UserResponse User);
    public record UserResponse(Guid Id, string Email, string FirstName, string LastName, string Role, bool EmailVerified);
    public record ProductResponse(Guid Id, string Name, string Description, decimal Price, decimal? SalePrice, string Era, string Category, string Size, string Condition, string ImageUrl, List<string> AdditionalImageUrls, List<string> VideoUrls, bool InStock, int ViewCount);
    public record OrderResponse(Guid Id, string Status, decimal Total, DateTime CreatedAtUtc, List<OrderItemResponse> Items);
    public record OrderItemResponse(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);
    public record ProfileResponse(Guid Id, string Email, string FirstName, string LastName, AddressResponse DeliveryAddress, AddressResponse BillingAddress, PaymentResponse? Payment, bool MfaEnabled, bool EmailVerified);
    public record AddressResponse(string? AddressLine1, string? AddressLine2, string? City, string? County, string? Postcode, string? Country);
    public record PaymentResponse(string? CardholderName, string? CardLast4, string? CardBrand, int? ExpiryMonth, int? ExpiryYear);
    public record MfaSetupResponse(string Secret, string QrUri);
    public record MessageResponse(string Message);
    public record TransactionResponse(Guid Id, DateTime Date, string Description, decimal Amount, string Category, string? Platform, string? Reference, string? ReceiptUrl, string? Notes, DateTime CreatedAtUtc);
    public record FinanceSummaryResponse(decimal TotalIncome, decimal TotalExpenses, decimal TotalProfit, int TransactionCount, List<MonthSummary> ByMonth);
    public record MonthSummary(string Month, decimal Income, decimal Expenses, decimal Profit, int Count);
    public record AnalyseImageError(string Error);
}
