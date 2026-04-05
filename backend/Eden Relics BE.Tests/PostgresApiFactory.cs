using Eden_Relics_BE.Data;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Test factory that uses a real PostgreSQL database via Testcontainers.
/// Catches provider-specific issues that InMemory tests miss (jsonb, migrations, etc).
/// </summary>
public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

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
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing Npgsql registration and re-add with the container connection string
            List<ServiceDescriptor> efServiceTypes = services
                .Where(d =>
                    d.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore") == true
                    || d.ServiceType.FullName?.Contains("Npgsql") == true
                    || d.ImplementationType?.FullName?.Contains("Npgsql") == true
                    || d.ServiceType == typeof(EdenRelicsDbContext))
                .ToList();
            foreach (ServiceDescriptor d in efServiceTypes)
            {
                services.Remove(d);
            }

            NpgsqlDataSourceBuilder dsBuilder = new(_postgres.GetConnectionString());
            dsBuilder.EnableDynamicJson();
            NpgsqlDataSource ds = dsBuilder.Build();

            services.AddDbContext<EdenRelicsDbContext>(options =>
                options.UseNpgsql(ds)
                    .ConfigureWarnings(w => w.Ignore(
                        Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

            // Replace email service with no-op
            ServiceDescriptor? emailDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor is not null)
            {
                services.Remove(emailDescriptor);
            }
            services.AddTransient<IEmailService, FakeEmailService>();

            // Remove background services
            services.RemoveAll(typeof(Microsoft.Extensions.Hosting.IHostedService));
        });
    }
}
