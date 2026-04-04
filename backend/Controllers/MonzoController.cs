using System.Text;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class MonzoController(
    EdenRelicsDbContext context,
    MonzoService monzo,
    ImageStorageService storage,
    IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult> GetStatus()
    {
        if (!monzo.IsOAuthConfigured)
        {
            return Ok(new { connected = false, pendingApproval = false, accountId = (string?)null });
        }

        MonzoToken? token = await context.MonzoTokens.FirstOrDefaultAsync();
        bool connected = token is not null && !string.IsNullOrEmpty(token.AccountId);
        bool pendingApproval = token is not null && string.IsNullOrEmpty(token.AccountId);

        return Ok(new { connected, pendingApproval, accountId = token?.AccountId });
    }

    [HttpGet("connect")]
    public ActionResult Connect()
    {
        if (!monzo.IsOAuthConfigured)
        {
            return BadRequest(new { error = "Monzo OAuth is not configured. Set Monzo:ClientId and Monzo:ClientSecret." });
        }

        string state = Guid.NewGuid().ToString("N");
        string url = monzo.GetAuthorizeUrl(state);
        return Ok(new { url, state });
    }

    [HttpPost("callback")]
    public async Task<ActionResult> Callback([FromBody] MonzoCallbackDto dto)
    {
        if (!monzo.IsOAuthConfigured)
        {
            return BadRequest(new { error = "Monzo OAuth is not configured." });
        }

        (MonzoTokenResponse? tokenResponse, string? exchangeError) = await monzo.ExchangeCodeAsync(dto.Code);
        if (tokenResponse is null)
        {
            return BadRequest(new { error = $"Failed to exchange authorization code: {exchangeError}" });
        }

        List<MonzoToken> existing = await context.MonzoTokens.ToListAsync();
        context.MonzoTokens.RemoveRange(existing);

        context.MonzoTokens.Add(new MonzoToken
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            AccountId = "",
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
        });

        await context.SaveChangesAsync();

        return Ok(new { message = "Token saved. Please approve access in the Monzo app, then click Verify.", pendingApproval = true });
    }

    [HttpPost("verify")]
    public async Task<ActionResult> Verify()
    {
        MonzoToken? token = await context.MonzoTokens.FirstOrDefaultAsync();
        if (token is null)
        {
            return BadRequest(new { error = "No Monzo token found. Please connect first." });
        }

        string? accessToken = await monzo.EnsureValidTokenAsync(context);
        if (accessToken is null)
        {
            return BadRequest(new { error = "Token expired. Please reconnect." });
        }

        List<MonzoAccountResponse> accounts = await monzo.GetAccountsAsync(accessToken);
        if (accounts.Count == 0)
        {
            return Ok(new { verified = false, message = "Waiting for approval in the Monzo app. Open the Monzo app and approve API access, then try again." });
        }

        token.AccountId = accounts.First().Id;
        await context.SaveChangesAsync();

        return Ok(new { verified = true, accountId = token.AccountId });
    }

    [HttpPost("disconnect")]
    public async Task<ActionResult> Disconnect()
    {
        List<MonzoToken> tokens = await context.MonzoTokens.ToListAsync();
        context.MonzoTokens.RemoveRange(tokens);
        await context.SaveChangesAsync();

        return Ok(new { message = "Monzo disconnected." });
    }

    [HttpGet("debug-transactions")]
    public async Task<ActionResult> DebugTransactions()
    {
        string? accessToken = await monzo.EnsureValidTokenAsync(context);
        MonzoToken? token = await context.MonzoTokens.FirstOrDefaultAsync();
        if (accessToken is null || token is null)
        {
            return BadRequest(new { error = "Monzo is not connected." });
        }

        // Try with no filters at all
        List<MonzoTransactionResponse> noFilter = await monzo.GetTransactionsAsync(accessToken, token.AccountId);
        // Try with a very recent since
        List<MonzoTransactionResponse> recent = await monzo.GetTransactionsAsync(accessToken, token.AccountId, since: DateTime.UtcNow.AddDays(-7));
        // Try with a longer range
        List<MonzoTransactionResponse> longer = await monzo.GetTransactionsAsync(accessToken, token.AccountId, since: DateTime.UtcNow.AddDays(-90));

        return Ok(new
        {
            accountId = token.AccountId,
            tokenExpiresAt = token.ExpiresAtUtc,
            noFilterCount = noFilter.Count,
            recentCount = recent.Count,
            longerCount = longer.Count,
            sampleTransaction = noFilter.FirstOrDefault(),
        });
    }

    [HttpGet("accounts")]
    public async Task<ActionResult> GetAccounts()
    {
        string? accessToken = await monzo.EnsureValidTokenAsync(context);
        if (accessToken is null)
        {
            return BadRequest(new { error = "Monzo is not connected." });
        }

        List<MonzoAccountResponse> accounts = await monzo.GetAccountsAsync(accessToken);
        return Ok(new { accounts });
    }

    [HttpGet("pots")]
    public async Task<ActionResult<List<MonzoPotDto>>> GetPots()
    {
        string? accessToken = await monzo.EnsureValidTokenAsync(context);
        MonzoToken? token = await context.MonzoTokens.FirstOrDefaultAsync();

        if (accessToken is null || token is null)
        {
            return BadRequest(new { error = "Monzo is not connected." });
        }

        List<MonzoPotResponse> pots = await monzo.GetPotsAsync(accessToken, token.AccountId);
        List<MonzoPotDto> result = pots
            .Select(p => new MonzoPotDto(p.Id, p.Name, p.Balance / 100m, p.Currency))
            .ToList();

        return Ok(result);
    }

    [HttpGet("balance")]
    public async Task<ActionResult<MonzoBalanceDto>> GetBalance()
    {
        string? accessToken = await monzo.EnsureValidTokenAsync(context);
        MonzoToken? token = await context.MonzoTokens.FirstOrDefaultAsync();

        if (accessToken is null || token is null)
        {
            return BadRequest(new { error = "Monzo is not connected." });
        }

        MonzoBalanceResponse? balance = await monzo.GetBalanceAsync(accessToken, token.AccountId);
        if (balance is null)
        {
            return BadRequest(new { error = "Failed to fetch balance from Monzo." });
        }

        return Ok(new MonzoBalanceDto(
            balance.Balance / 100m,
            balance.TotalBalance / 100m,
            balance.Currency,
            balance.SpendToday / 100m));
    }

    [HttpPost("sync")]
    public async Task<ActionResult> Sync()
    {
        string? accessToken = await monzo.EnsureValidTokenAsync(context);
        MonzoToken? token = await context.MonzoTokens.FirstOrDefaultAsync();

        if (accessToken is null || token is null)
        {
            return BadRequest(new { error = "Monzo is not connected." });
        }

        try
        {
            var (fetched, added, accountId, since) = await MonzoSyncBackgroundService.SyncTransactionsAsync(monzo, context, accessToken, token.AccountId);
            return Ok(new { message = "Sync completed.", fetched, added, accountId, since });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<List<MonzoTransactionDto>>> GetTransactions(
        [FromQuery] int? year, [FromQuery] int? month)
    {
        IQueryable<MonzoTransaction> query = context.MonzoTransactions
            .OrderByDescending(t => t.Date);

        if (year.HasValue)
        {
            query = query.Where(t => t.Date.Year == year.Value);
        }
        if (month.HasValue)
        {
            query = query.Where(t => t.Date.Month == month.Value);
        }

        List<MonzoTransactionDto> transactions = await query
            .Select(t => ToDto(t))
            .ToListAsync();

        return Ok(transactions);
    }

    [HttpPatch("transactions/{id:guid}/annotate")]
    public async Task<ActionResult<MonzoTransactionDto>> Annotate(Guid id, MonzoAnnotateDto dto)
    {
        MonzoTransaction? txn = await context.MonzoTransactions.FindAsync(id);
        if (txn is null)
        {
            return NotFound();
        }

        if (dto.Notes is not null) { txn.Notes = dto.Notes; }
        if (dto.Tags is not null) { txn.Tags = dto.Tags; }
        if (dto.UserCategory is not null) { txn.UserCategory = dto.UserCategory == "" ? null : dto.UserCategory; }
        if (dto.Platform is not null) { txn.Platform = dto.Platform == "" ? null : dto.Platform; }

        await context.SaveChangesAsync();

        string? accessToken = await monzo.EnsureValidTokenAsync(context);
        if (accessToken is not null)
        {
            await monzo.AnnotateTransactionAsync(accessToken, txn.MonzoId, dto.Notes, dto.Tags);
        }

        return Ok(ToDto(txn));
    }

    [HttpPost("transactions/{id:guid}/upload-receipt")]
    public async Task<ActionResult<MonzoTransactionDto>> UploadReceipt(Guid id, IFormFile file)
    {
        MonzoTransaction? txn = await context.MonzoTransactions.FindAsync(id);
        if (txn is null)
        {
            return NotFound();
        }

        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Only image files (jpg, png, webp) and PDFs are allowed." });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { error = "File size must be under 10MB." });
        }

        string receiptUrl = await ImageUploadHelper.ProcessAndUploadAsync(
            file.OpenReadStream(), storage, env, Request, "receipts", maxWidth: 1200, maxHeight: 1600, quality: 80);

        txn.ReceiptUrl = receiptUrl;
        await context.SaveChangesAsync();

        return Ok(ToDto(txn));
    }

    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary()
    {
        List<MonzoTransaction> all = await context.MonzoTransactions.ToListAsync();

        var byMonth = all
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g =>
            {
                decimal income = g.Where(t => t.Amount > 0).Sum(t => t.Amount);
                decimal expenses = g.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
                return new
                {
                    month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    income,
                    expenses,
                    profit = income - expenses,
                    count = g.Count(),
                    byCategory = g
                        .Where(t => t.UserCategory is not null)
                        .GroupBy(t => t.UserCategory!)
                        .Select(c => new { category = c.Key, total = c.Sum(t => t.Amount), count = c.Count() })
                        .OrderByDescending(c => Math.Abs(c.total))
                        .ToList(),
                    byPlatform = g
                        .Where(t => t.Platform is not null)
                        .GroupBy(t => t.Platform!)
                        .Select(p => new { platform = p.Key, total = p.Sum(t => t.Amount), count = p.Count() })
                        .OrderByDescending(p => Math.Abs(p.total))
                        .ToList(),
                };
            })
            .ToList();

        decimal totalIncome = all.Where(t => t.Amount > 0).Sum(t => t.Amount);
        decimal totalExpenses = all.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
        int tagged = all.Count(t => t.UserCategory is not null);

        return Ok(new
        {
            totalIncome = Math.Round(totalIncome, 2),
            totalExpenses = Math.Round(totalExpenses, 2),
            totalProfit = Math.Round(totalIncome - totalExpenses, 2),
            transactionCount = all.Count,
            taggedCount = tagged,
            untaggedCount = all.Count - tagged,
            byMonth,
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] int? year, [FromQuery] int? month)
    {
        IQueryable<MonzoTransaction> query = context.MonzoTransactions.OrderByDescending(t => t.Date);

        if (year.HasValue)
        {
            query = query.Where(t => t.Date.Year == year.Value);
        }
        if (month.HasValue)
        {
            query = query.Where(t => t.Date.Month == month.Value);
        }

        List<MonzoTransaction> transactions = await query.ToListAsync();

        StringBuilder csv = new();
        csv.AppendLine("Date,Description,Amount,Monzo Category,Tagged Category,Platform,Merchant,Notes,Settled,Receipt");
        foreach (MonzoTransaction t in transactions)
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

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    private static string Escape(string value) => value.Replace("\"", "\"\"");

    private static MonzoTransactionDto ToDto(MonzoTransaction t) => new(
        t.Id, t.MonzoId, t.Date, t.Description, t.Amount, t.Currency,
        t.Category, t.MerchantName, t.MerchantLogo, t.Notes, t.Tags,
        t.IsLoad, t.DeclineReason, t.SettledAt,
        t.UserCategory, t.Platform, t.ReceiptUrl, t.CreatedAtUtc);
}
