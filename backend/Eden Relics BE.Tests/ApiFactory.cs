using Eden_Relics_BE.Data;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL EF Core service registrations to avoid provider conflicts
            var efServiceTypes = services
                .Where(d =>
                    d.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore") == true
                    || d.ServiceType.FullName?.Contains("Npgsql") == true
                    || d.ImplementationType?.FullName?.Contains("Npgsql") == true
                    || d.ServiceType == typeof(EdenRelicsDbContext))
                .ToList();
            foreach (var d in efServiceTypes)
                services.Remove(d);

            services.AddDbContext<EdenRelicsDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Replace email service with no-op
            var emailDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor is not null)
                services.Remove(emailDescriptor);
            services.AddTransient<IEmailService, FakeEmailService>();
        });
    }
}

public class FakeEmailService : IEmailService
{
    public Task SendVerificationEmailAsync(string toEmail, string firstName, string token) => Task.CompletedTask;
    public Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token) => Task.CompletedTask;
    public Task SendContactEmailAsync(string fromName, string fromEmail, string subject, string message) => Task.CompletedTask;
}
