using System.Text;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Eden_Relics_BE.Services;

// Orchestrates the Monzo integration: token storage/refresh/rotation, OAuth state,
// transaction sync, and reporting. All persistence goes through repositories; the raw
// REST calls live in MonzoApiClient.
public class MonzoService(
    MonzoApiClient api,
    IRepository<MonzoToken> tokens,
    IRepository<MonzoTransaction> transactions,
    IDistributedCache cache,
    ILogger<MonzoService> logger) : IMonzoService
{
    private const string OAuthStateKeyPrefix = "monzo_oauth_state:";

    // --- Connection lifecycle ---

    public async Task<MonzoStatusDto> GetStatusAsync()
    {
        if (!api.IsOAuthConfigured)
        {
            return new MonzoStatusDto(Connected: false, PendingApproval: false, AccountId: null);
        }

        MonzoToken? token = await tokens.Query().FirstOrDefaultAsync();
        bool connected = token is not null && !string.IsNullOrEmpty(token.AccountId);
        bool pendingApproval = token is not null && string.IsNullOrEmpty(token.AccountId);

        return new MonzoStatusDto(connected, pendingApproval, token?.AccountId);
    }

    public async Task<MonzoConnectResult> ConnectAsync()
    {
        if (!api.IsOAuthConfigured)
        {
            return new MonzoConnectResult(MonzoConnectOutcome.NotConfigured, null, null);
        }

        string state = Guid.NewGuid().ToString("N");
        // Remember the state we issued so the callback can confirm it's one we started
        // (CSRF protection for the OAuth round-trip).
        await cache.SetStringAsync(
            OAuthStateKeyPrefix + state,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });

        string url = api.GetAuthorizeUrl(state);
        return new MonzoConnectResult(MonzoConnectOutcome.Success, url, state);
    }

    public async Task<MonzoCallbackResult> CompleteCallbackAsync(MonzoCallbackDto dto)
    {
        if (!api.IsOAuthConfigured)
        {
            return new MonzoCallbackResult(MonzoCallbackOutcome.NotConfigured, null);
        }

        // Confirm the state matches one we issued (and consume it) before exchanging the code.
        string stateKey = OAuthStateKeyPrefix + dto.State;
        if (string.IsNullOrEmpty(dto.State) || await cache.GetStringAsync(stateKey) is null)
        {
            return new MonzoCallbackResult(MonzoCallbackOutcome.InvalidState, null);
        }
        await cache.RemoveAsync(stateKey);

        (MonzoTokenResponse? tokenResponse, string? exchangeError) = await api.ExchangeCodeAsync(dto.Code);
        if (tokenResponse is null)
        {
            return new MonzoCallbackResult(MonzoCallbackOutcome.ExchangeFailed, exchangeError);
        }

        // Rotate: tokens are IHardDeletable, so the old rows are physically removed.
        List<MonzoToken> existing = await tokens.Query().ToListAsync();
        await tokens.RemoveRangeAsync(existing);

        await tokens.AddAsync(new MonzoToken
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            AccountId = "",
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
        });

        return new MonzoCallbackResult(MonzoCallbackOutcome.Success, null);
    }

    public async Task<MonzoVerifyResult> VerifyAsync()
    {
        MonzoToken? token = await tokens.Query().FirstOrDefaultAsync();
        if (token is null)
        {
            return new MonzoVerifyResult(MonzoVerifyOutcome.NoToken, null);
        }

        string? accessToken = await EnsureValidAccessTokenAsync(token);
        if (accessToken is null)
        {
            return new MonzoVerifyResult(MonzoVerifyOutcome.TokenExpired, null);
        }

        List<MonzoAccountResponse> accounts = await api.GetAccountsAsync(accessToken);
        if (accounts.Count == 0)
        {
            return new MonzoVerifyResult(MonzoVerifyOutcome.AwaitingApproval, null);
        }

        token.AccountId = accounts.First().Id;
        await tokens.UpdateAsync(token);

        return new MonzoVerifyResult(MonzoVerifyOutcome.Verified, token.AccountId);
    }

    public async Task DisconnectAsync()
    {
        List<MonzoToken> all = await tokens.Query().ToListAsync();
        await tokens.RemoveRangeAsync(all);
    }

    // --- Read-through Monzo API endpoints ---

    public async Task<MonzoDebugDto?> DebugTransactionsAsync()
    {
        (string? accessToken, MonzoToken? token) = await EnsureConnectionAsync();
        if (accessToken is null || token is null)
        {
            return null;
        }

        // Try with no filters at all
        List<MonzoTransactionResponse> noFilter = await api.GetTransactionsAsync(accessToken, token.AccountId);
        // Try with a very recent since
        List<MonzoTransactionResponse> recent = await api.GetTransactionsAsync(accessToken, token.AccountId, since: DateTime.UtcNow.AddDays(-7));
        // Try with a longer range
        List<MonzoTransactionResponse> longer = await api.GetTransactionsAsync(accessToken, token.AccountId, since: DateTime.UtcNow.AddDays(-90));

        return new MonzoDebugDto(
            token.AccountId,
            token.ExpiresAtUtc,
            noFilter.Count,
            recent.Count,
            longer.Count,
            noFilter.FirstOrDefault());
    }

    public async Task<List<MonzoAccountResponse>?> GetAccountsAsync()
    {
        (string? accessToken, _) = await EnsureConnectionAsync();
        if (accessToken is null)
        {
            return null;
        }

        return await api.GetAccountsAsync(accessToken);
    }

    public async Task<List<MonzoPotDto>?> GetPotsAsync()
    {
        (string? accessToken, MonzoToken? token) = await EnsureConnectionAsync();
        if (accessToken is null || token is null)
        {
            return null;
        }

        List<MonzoPotResponse> pots = await api.GetPotsAsync(accessToken, token.AccountId);
        return pots
            .Select(p => new MonzoPotDto(p.Id, p.Name, p.Balance / 100m, p.Currency))
            .ToList();
    }

    public async Task<MonzoBalanceResult> GetBalanceAsync()
    {
        (string? accessToken, MonzoToken? token) = await EnsureConnectionAsync();
        if (accessToken is null || token is null)
        {
            return new MonzoBalanceResult(MonzoBalanceOutcome.NotConnected, null);
        }

        MonzoBalanceResponse? balance = await api.GetBalanceAsync(accessToken, token.AccountId);
        if (balance is null)
        {
            return new MonzoBalanceResult(MonzoBalanceOutcome.FetchFailed, null);
        }

        return new MonzoBalanceResult(MonzoBalanceOutcome.Success, new MonzoBalanceDto(
            balance.Balance / 100m,
            balance.TotalBalance / 100m,
            balance.Currency,
            balance.SpendToday / 100m));
    }

    // --- Sync ---

    public async Task<MonzoSyncResult?> SyncAsync()
    {
        (string? accessToken, MonzoToken? token) = await EnsureConnectionAsync();
        if (accessToken is null || token is null)
        {
            return null;
        }

        return await SyncTransactionsAsync(accessToken, token.AccountId);
    }

    public async Task RunScheduledSyncAsync()
    {
        (string? accessToken, MonzoToken? token) = await EnsureConnectionAsync();
        if (accessToken is not null && token is not null)
        {
            await SyncTransactionsAsync(accessToken, token.AccountId);
            logger.LogInformation("Monzo sync completed");
        }
    }

    private async Task<MonzoSyncResult> SyncTransactionsAsync(string accessToken, string accountId)
    {
        DateTime? latestDate = await transactions.Query()
            .OrderByDescending(t => t.Date)
            .Select(t => (DateTime?)t.Date)
            .FirstOrDefaultAsync();

        // Only pass 'since' for incremental syncs — Monzo returns empty for far-back dates
        DateTime? since = latestDate?.AddMinutes(-5);

        List<MonzoTransactionResponse> fetched = await api.GetTransactionsAsync(
            accessToken, accountId, since: since);

        // Build pot ID → name lookup for friendly descriptions
        List<MonzoPotResponse> pots = await api.GetPotsAsync(accessToken, accountId);
        Dictionary<string, string> potNames = pots.ToDictionary(p => p.Id, p => p.Name);

        List<MonzoTransaction> toAdd = [];
        foreach (MonzoTransactionResponse txn in fetched)
        {
            bool exists = await transactions.Query().AnyAsync(t => t.MonzoId == txn.Id);
            if (exists) { continue; }

            toAdd.Add(new MonzoTransaction
            {
                MonzoId = txn.Id,
                Date = txn.Created.ToUniversalTime(),
                Description = Truncate(txn.Merchant?.Name ?? FormatDescription(txn.Description, potNames), 500)!,
                Amount = txn.Amount / 100m,
                Currency = txn.Currency,
                Category = FormatCategory(txn.Category),
                MerchantName = Truncate(txn.Merchant?.Name, 200),
                MerchantLogo = Truncate(txn.Merchant?.Logo, 2000),
                Notes = Truncate(txn.Notes, 1000),
                Tags = Truncate(txn.Metadata?.GetValueOrDefault("tags"), 500),
                IsLoad = txn.IsLoad,
                DeclineReason = Truncate(txn.DeclineReason, 100),
                SettledAt = DateTime.TryParse(txn.Settled, out DateTime settled) ? settled.ToUniversalTime() : null,
            });
        }

        // Fix existing transactions that still have raw pot IDs in their description
        if (potNames.Count > 0)
        {
            List<MonzoTransaction> potTransactions = await transactions.Query()
                .Where(t => t.Description.StartsWith("Pot ") && t.Description.Length > 10)
                .ToListAsync();

            foreach (MonzoTransaction existing in potTransactions)
            {
                string potId = "pot_" + existing.Description[4..].ToLower();
                if (potNames.TryGetValue(potId, out string? name))
                {
                    existing.Description = name;
                }
            }
        }

        // AddRangeAsync issues a single SaveChanges — this also flushes the tracked
        // pot-description edits above, even when there are no new rows to add.
        await transactions.AddRangeAsync(toAdd);
        return new MonzoSyncResult(fetched.Count, toAdd.Count, accountId, since);
    }

    // --- Transactions ---

    public async Task<List<MonzoTransactionDto>> GetTransactionsAsync(int? year, int? month)
    {
        IQueryable<MonzoTransaction> query = transactions.Query()
            .OrderByDescending(t => t.Date);

        if (year.HasValue)
        {
            query = query.Where(t => t.Date.Year == year.Value);
        }
        if (month.HasValue)
        {
            query = query.Where(t => t.Date.Month == month.Value);
        }

        List<MonzoTransaction> rows = await query.ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<MonzoTransactionDto?> AnnotateAsync(Guid id, MonzoAnnotateDto dto)
    {
        MonzoTransaction? txn = await transactions.GetByIdAsync(id);
        if (txn is null)
        {
            return null;
        }

        if (dto.Notes is not null) { txn.Notes = dto.Notes; }
        if (dto.Tags is not null) { txn.Tags = dto.Tags; }
        if (dto.UserCategory is not null) { txn.UserCategory = dto.UserCategory == "" ? null : dto.UserCategory; }
        if (dto.Platform is not null) { txn.Platform = dto.Platform == "" ? null : dto.Platform; }

        await transactions.UpdateAsync(txn);

        // Best-effort: mirror notes/tags back to Monzo if we still hold a valid token.
        (string? accessToken, _) = await EnsureConnectionAsync();
        if (accessToken is not null)
        {
            await api.AnnotateTransactionAsync(accessToken, txn.MonzoId, dto.Notes, dto.Tags);
        }

        return ToDto(txn);
    }

    public async Task<MonzoTransactionDto?> SetReceiptUrlAsync(Guid id, string receiptUrl)
    {
        MonzoTransaction? txn = await transactions.GetByIdAsync(id);
        if (txn is null)
        {
            return null;
        }

        txn.ReceiptUrl = receiptUrl;
        await transactions.UpdateAsync(txn);
        return ToDto(txn);
    }

    // --- Reporting ---

    public async Task<MonzoSummaryDto> GetSummaryAsync()
    {
        List<MonzoTransaction> all = await transactions.Query().ToListAsync();

        List<MonzoMonthSummaryDto> byMonth = all
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g =>
            {
                decimal income = g.Where(t => t.Amount > 0).Sum(t => t.Amount);
                decimal expenses = g.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
                return new MonzoMonthSummaryDto(
                    Month: $"{g.Key.Year}-{g.Key.Month:D2}",
                    Income: income,
                    Expenses: expenses,
                    Profit: income - expenses,
                    Count: g.Count(),
                    ByCategory: g
                        .Where(t => t.UserCategory is not null)
                        .GroupBy(t => t.UserCategory!)
                        .Select(c => new MonzoCategorySummaryDto(c.Key, c.Sum(t => t.Amount), c.Count()))
                        .OrderByDescending(c => Math.Abs(c.Total))
                        .ToList(),
                    ByPlatform: g
                        .Where(t => t.Platform is not null)
                        .GroupBy(t => t.Platform!)
                        .Select(p => new MonzoPlatformSummaryDto(p.Key, p.Sum(t => t.Amount), p.Count()))
                        .OrderByDescending(p => Math.Abs(p.Total))
                        .ToList());
            })
            .ToList();

        decimal totalIncome = all.Where(t => t.Amount > 0).Sum(t => t.Amount);
        decimal totalExpenses = all.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
        int tagged = all.Count(t => t.UserCategory is not null);

        return new MonzoSummaryDto(
            TotalIncome: Math.Round(totalIncome, 2),
            TotalExpenses: Math.Round(totalExpenses, 2),
            TotalProfit: Math.Round(totalIncome - totalExpenses, 2),
            TransactionCount: all.Count,
            TaggedCount: tagged,
            UntaggedCount: all.Count - tagged,
            ByMonth: byMonth);
    }

    public async Task<MonzoExportFile> ExportAsync(int? year, int? month)
    {
        IQueryable<MonzoTransaction> query = transactions.Query().OrderByDescending(t => t.Date);

        if (year.HasValue)
        {
            query = query.Where(t => t.Date.Year == year.Value);
        }
        if (month.HasValue)
        {
            query = query.Where(t => t.Date.Month == month.Value);
        }

        List<MonzoTransaction> rows = await query.ToListAsync();

        StringBuilder csv = new();
        csv.AppendLine("Date,Description,Amount,Monzo Category,Tagged Category,Platform,Merchant,Notes,Settled,Receipt");
        foreach (MonzoTransaction t in rows)
        {
            csv.AppendLine(
                $"{t.Date:yyyy-MM-dd}," +
                $"\"{Escape(t.Description)}\"," +
                $"{t.Amount}," +
                $"\"{Escape(t.Category)}\"," +
                $"\"{Escape(t.UserCategory ?? "")}\"," +
                $"\"{Escape(t.Platform ?? "")}\"," +
                $"\"{Escape(t.MerchantName ?? "")}\"," +
                $"\"{Escape(t.Notes ?? "")}\"," +
                $"{(t.SettledAt.HasValue ? t.SettledAt.Value.ToString("yyyy-MM-dd") : "")}," +
                $"\"{Escape(t.ReceiptUrl ?? "")}\"");
        }

        string fileName = year.HasValue && month.HasValue
            ? $"monzo-{year}-{month:D2}.csv"
            : year.HasValue
                ? $"monzo-{year}.csv"
                : "monzo-all.csv";

        return new MonzoExportFile(Encoding.UTF8.GetBytes(csv.ToString()), fileName);
    }

    // --- Token management ---

    // Returns a valid access token + the token row, refreshing if near expiry.
    // AccessToken is null when there is no token OR a refresh failed; Token is null only
    // when no token row exists.
    private async Task<(string? AccessToken, MonzoToken? Token)> EnsureConnectionAsync()
    {
        MonzoToken? token = await tokens.Query().FirstOrDefaultAsync();
        if (token is null)
        {
            return (null, null);
        }

        string? accessToken = await EnsureValidAccessTokenAsync(token);
        return (accessToken, token);
    }

    private async Task<string?> EnsureValidAccessTokenAsync(MonzoToken token)
    {
        if (token.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(5))
        {
            return token.AccessToken;
        }

        MonzoTokenResponse? refreshed = await api.RefreshTokenAsync(token.RefreshToken);
        if (refreshed is null)
        {
            logger.LogWarning("Failed to refresh Monzo token — connection may need re-authorizing");
            return null;
        }

        token.AccessToken = refreshed.AccessToken;
        token.RefreshToken = refreshed.RefreshToken;
        token.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);
        await tokens.UpdateAsync(token);

        return token.AccessToken;
    }

    // --- Helpers ---

    private static string FormatDescription(string description, Dictionary<string, string> potNames)
    {
        if (string.IsNullOrWhiteSpace(description)) { return description; }

        // Monzo pot transfers have descriptions like "pot_0000b2bflcp416rntjda7d"
        if (description.StartsWith("pot_", StringComparison.OrdinalIgnoreCase) && potNames.TryGetValue(description.ToLower(), out string? potName))
        {
            return potName;
        }

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

    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null) { return null; }
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string Escape(string value)
    {
        string escaped = value.Replace("\"", "\"\"");
        // Neutralise spreadsheet formula injection (cells starting with = + - @).
        if (escaped.Length > 0 && escaped[0] is '=' or '+' or '-' or '@')
        {
            escaped = "'" + escaped;
        }
        return escaped;
    }

    private static MonzoTransactionDto ToDto(MonzoTransaction t) => new(
        t.Id, t.MonzoId, t.Date, t.Description, t.Amount, t.Currency,
        t.Category, t.MerchantName, t.MerchantLogo, t.Notes, t.Tags,
        t.IsLoad, t.DeclineReason, t.SettledAt,
        t.UserCategory, t.Platform, t.ReceiptUrl, t.CreatedAtUtc);
}
