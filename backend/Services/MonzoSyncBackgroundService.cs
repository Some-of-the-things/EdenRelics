using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class MonzoSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MonzoSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                MonzoService monzo = scope.ServiceProvider.GetRequiredService<MonzoService>();
                EdenRelicsDbContext context = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

                string? accessToken = await monzo.EnsureValidTokenAsync(context);
                MonzoToken? token = await context.MonzoTokens.FirstOrDefaultAsync(stoppingToken);

                if (accessToken is not null && token is not null)
                {
                    await SyncTransactionsAsync(monzo, context, accessToken, token.AccountId);
                    logger.LogInformation("Monzo sync completed");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Monzo sync failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    public static async Task SyncTransactionsAsync(
        MonzoService monzo, EdenRelicsDbContext context, string accessToken, string accountId)
    {
        DateTime? latestDate = await context.MonzoTransactions
            .OrderByDescending(t => t.Date)
            .Select(t => (DateTime?)t.Date)
            .FirstOrDefaultAsync();

        DateTime since = latestDate?.AddMinutes(-5) ?? DateTime.UtcNow.AddDays(-90);

        List<MonzoTransactionResponse> transactions = await monzo.GetTransactionsAsync(
            accessToken, accountId, since: since);

        foreach (MonzoTransactionResponse txn in transactions)
        {
            bool exists = await context.MonzoTransactions
                .AnyAsync(t => t.MonzoId == txn.Id);

            if (exists) { continue; }

            context.MonzoTransactions.Add(new MonzoTransaction
            {
                MonzoId = txn.Id,
                Date = txn.Created.ToUniversalTime(),
                Description = txn.Merchant?.Name ?? FormatDescription(txn.Description),
                Amount = txn.Amount / 100m,
                Currency = txn.Currency,
                Category = FormatCategory(txn.Category),
                MerchantName = txn.Merchant?.Name,
                MerchantLogo = txn.Merchant?.Logo,
                Notes = txn.Notes,
                Tags = txn.Metadata?.GetValueOrDefault("tags"),
                IsLoad = txn.IsLoad,
                DeclineReason = txn.DeclineReason,
                SettledAt = DateTime.TryParse(txn.Settled, out DateTime settled) ? settled.ToUniversalTime() : null,
            });
        }

        await context.SaveChangesAsync();
    }

    private static string FormatDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) { return description; }
        return string.Join(' ', description
            .Replace("_", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length > 1
                ? char.ToUpper(w[0]) + w[1..].ToLower()
                : w.ToUpper()));
    }

    private static string FormatCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) { return "General"; }
        return string.Join(' ', category
            .Replace("_", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length > 1
                ? char.ToUpper(w[0]) + w[1..].ToLower()
                : w.ToUpper()));
    }
}
