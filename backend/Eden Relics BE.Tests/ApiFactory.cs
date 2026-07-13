using System.Text.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Eden_Relics_BE.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "TestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TestSecretKeyThatIsAtLeast32CharsLong!!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Stripe:SecretKey"] = "",
                ["Stripe:WebhookSecret"] = "",
                ["Stripe:FrontendUrl"] = "http://localhost:4200",
                ["Email:ResendApiKey"] = "",
                ["Email:From"] = "Test <test@test.com>",
                ["Email:FrontendUrl"] = "http://localhost:4200",
                ["Fido2:ServerDomain"] = "localhost",
                ["Fido2:Origins:0"] = "http://localhost:4200",
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
                ["Analytics:IngestSecret"] = "test-analytics-secret",
                ["Marketplace:Enabled"] = "true",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL EF Core service registrations to avoid provider conflicts
            List<ServiceDescriptor> efServiceTypes = services
                .Where(d =>
                    d.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore") == true
                    || d.ServiceType.FullName?.Contains("Npgsql") == true
                    || d.ImplementationType?.FullName?.Contains("Npgsql") == true
                    || d.ServiceType == typeof(EdenRelicsDbContext))
                .ToList();
            foreach (ServiceDescriptor d in efServiceTypes)
                services.Remove(d);

            services.AddDbContext<EdenRelicsDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Replace email service with no-op
            ServiceDescriptor? emailDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor is not null)
            {
                services.Remove(emailDescriptor);
            }
            services.AddTransient<IEmailService, FakeEmailService>();

            // Replace Stripe Connect with a fake so onboarding tests don't hit Stripe.
            ServiceDescriptor? connectDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IStripeConnectService));
            if (connectDescriptor is not null)
            {
                services.Remove(connectDescriptor);
            }
            services.AddScoped<IStripeConnectService, FakeStripeConnectService>();

            // Remove background services that interfere with test host disposal
            services.RemoveAll(typeof(Microsoft.Extensions.Hosting.IHostedService));

        });
    }
}

public class FakeStripeConnectService : IStripeConnectService
{
    public Task<string> CreateAccountAsync(string? email) =>
        Task.FromResult("acct_fake_" + Guid.NewGuid().ToString("N")[..12]);

    public Task<string> CreateAccountLinkAsync(string connectedAccountId, string returnUrl, string refreshUrl) =>
        Task.FromResult($"https://connect.stripe.test/onboarding/{connectedAccountId}");

    // Pretend the account finishes onboarding immediately (charges + payouts enabled).
    public Task<(bool ChargesEnabled, bool PayoutsEnabled)> GetAccountStatusAsync(string connectedAccountId) =>
        Task.FromResult((true, true));
}

public class FakeEmailService : IEmailService
{
    public Task SendVerificationEmailAsync(string toEmail, string firstName, string token) => Task.CompletedTask;
    public Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token) => Task.CompletedTask;
    public Task SendContactEmailAsync(string fromName, string fromEmail, string subject, string message) => Task.CompletedTask;
    public Task SendSaleNotificationAsync(string toEmail, string firstName, string productName, decimal originalPrice, decimal salePrice) => Task.CompletedTask;
    public Task SendReviewRequestEmailAsync(string toEmail, string firstName, Guid orderId) => Task.CompletedTask;
    public Task SendDiscountWelcomeEmailAsync(string toEmail, string code) => Task.CompletedTask;
    public Task SendOwnerSaleNotificationAsync(Eden_Relics_BE.Data.Entities.Order order) => Task.CompletedTask;
    public Task SendOrderInvoiceEmailAsync(Eden_Relics_BE.Data.Entities.Order order, string? platform = null) => Task.CompletedTask;
    public string BuildOrderInvoiceHtml(Eden_Relics_BE.Data.Entities.Order order, string? platform = null) => string.Empty;
    public Task SendOperatorReminderEmailAsync(string toEmail, string title, string body) => Task.CompletedTask;
}
