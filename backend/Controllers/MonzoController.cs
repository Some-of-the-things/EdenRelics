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
    IConfiguration configuration) : ControllerBase
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

        MonzoTokenResponse? tokenResponse = await monzo.ExchangeCodeAsync(dto.Code);
        if (tokenResponse is null)
        {
            return BadRequest(new { error = "Failed to exchange authorization code." });
        }

        // Store the token immediately — account ID will be resolved on verify
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

        // Try to fetch accounts — this will fail with 403 until the user approves in the app
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

        await MonzoSyncBackgroundService.SyncTransactionsAsync(monzo, context, accessToken, token.AccountId);
        return Ok(new { message = "Sync completed." });
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

        await context.SaveChangesAsync();

        string? accessToken = await monzo.EnsureValidTokenAsync(context);
        if (accessToken is not null)
        {
            await monzo.AnnotateTransactionAsync(accessToken, txn.MonzoId, dto.Notes, dto.Tags);
        }

        return Ok(ToDto(txn));
    }

    private static MonzoTransactionDto ToDto(MonzoTransaction t) => new(
        t.Id, t.MonzoId, t.Date, t.Description, t.Amount, t.Currency,
        t.Category, t.MerchantName, t.MerchantLogo, t.Notes, t.Tags,
        t.IsLoad, t.DeclineReason, t.SettledAt, t.CreatedAtUtc);
}
