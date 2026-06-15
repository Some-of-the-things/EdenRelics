using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class MonzoController(
    IMonzoService monzo,
    ImageStorageService storage,
    IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult> GetStatus()
    {
        MonzoStatusDto status = await monzo.GetStatusAsync();
        return Ok(new { connected = status.Connected, pendingApproval = status.PendingApproval, accountId = status.AccountId });
    }

    [HttpGet("connect")]
    public async Task<ActionResult> Connect()
    {
        MonzoConnectResult result = await monzo.ConnectAsync();
        return result.Outcome switch
        {
            MonzoConnectOutcome.NotConfigured => BadRequest(new { error = "Monzo OAuth is not configured. Set Monzo:ClientId and Monzo:ClientSecret." }),
            _ => Ok(new { url = result.Url, state = result.State }),
        };
    }

    [HttpPost("callback")]
    public async Task<ActionResult> Callback([FromBody] MonzoCallbackDto dto)
    {
        MonzoCallbackResult result = await monzo.CompleteCallbackAsync(dto);
        return result.Outcome switch
        {
            MonzoCallbackOutcome.NotConfigured => BadRequest(new { error = "Monzo OAuth is not configured." }),
            MonzoCallbackOutcome.InvalidState => BadRequest(new { error = "Invalid or expired OAuth state. Please start the connection again." }),
            MonzoCallbackOutcome.ExchangeFailed => BadRequest(new { error = $"Failed to exchange authorization code: {result.ExchangeError}" }),
            _ => Ok(new { message = "Token saved. Please approve access in the Monzo app, then click Verify.", pendingApproval = true }),
        };
    }

    [HttpPost("verify")]
    public async Task<ActionResult> Verify()
    {
        MonzoVerifyResult result = await monzo.VerifyAsync();
        return result.Outcome switch
        {
            MonzoVerifyOutcome.NoToken => BadRequest(new { error = "No Monzo token found. Please connect first." }),
            MonzoVerifyOutcome.TokenExpired => BadRequest(new { error = "Token expired. Please reconnect." }),
            MonzoVerifyOutcome.AwaitingApproval => Ok(new { verified = false, message = "Waiting for approval in the Monzo app. Open the Monzo app and approve API access, then try again." }),
            _ => Ok(new { verified = true, accountId = result.AccountId }),
        };
    }

    [HttpPost("disconnect")]
    public async Task<ActionResult> Disconnect()
    {
        await monzo.DisconnectAsync();
        return Ok(new { message = "Monzo disconnected." });
    }

    [HttpGet("debug-transactions")]
    public async Task<ActionResult> DebugTransactions()
    {
        MonzoDebugDto? debug = await monzo.DebugTransactionsAsync();
        if (debug is null)
        {
            return BadRequest(new { error = "Monzo is not connected." });
        }

        return Ok(new
        {
            accountId = debug.AccountId,
            tokenExpiresAt = debug.TokenExpiresAt,
            noFilterCount = debug.NoFilterCount,
            recentCount = debug.RecentCount,
            longerCount = debug.LongerCount,
            sampleTransaction = debug.SampleTransaction,
        });
    }

    [HttpGet("accounts")]
    public async Task<ActionResult> GetAccounts()
    {
        List<MonzoAccountResponse>? accounts = await monzo.GetAccountsAsync();
        if (accounts is null)
        {
            return BadRequest(new { error = "Monzo is not connected." });
        }

        return Ok(new { accounts });
    }

    [HttpGet("pots")]
    public async Task<ActionResult<List<MonzoPotDto>>> GetPots()
    {
        List<MonzoPotDto>? pots = await monzo.GetPotsAsync();
        if (pots is null)
        {
            return BadRequest(new { error = "Monzo is not connected." });
        }

        return Ok(pots);
    }

    [HttpGet("balance")]
    public async Task<ActionResult<MonzoBalanceDto>> GetBalance()
    {
        MonzoBalanceResult result = await monzo.GetBalanceAsync();
        return result.Outcome switch
        {
            MonzoBalanceOutcome.NotConnected => BadRequest(new { error = "Monzo is not connected." }),
            MonzoBalanceOutcome.FetchFailed => BadRequest(new { error = "Failed to fetch balance from Monzo." }),
            _ => Ok(result.Balance),
        };
    }

    [HttpPost("sync")]
    public async Task<ActionResult> Sync()
    {
        try
        {
            MonzoSyncResult? result = await monzo.SyncAsync();
            if (result is null)
            {
                return BadRequest(new { error = "Monzo is not connected." });
            }

            return Ok(new { message = "Sync completed.", fetched = result.Fetched, added = result.Added, accountId = result.AccountId, since = result.Since });
        }
        catch
        {
            return StatusCode(500, new { error = "Sync failed. Please try again." });
        }
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<List<MonzoTransactionDto>>> GetTransactions(
        [FromQuery] int? year, [FromQuery] int? month)
    {
        return Ok(await monzo.GetTransactionsAsync(year, month));
    }

    [HttpPatch("transactions/{id:guid}/annotate")]
    public async Task<ActionResult<MonzoTransactionDto>> Annotate(Guid id, MonzoAnnotateDto dto)
    {
        MonzoTransactionDto? result = await monzo.AnnotateAsync(id, dto);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("transactions/{id:guid}/upload-receipt")]
    public async Task<ActionResult<MonzoTransactionDto>> UploadReceipt(Guid id, IFormFile file)
    {
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

        string receiptUrl = await ImageUploadHelper.ProcessAndUploadSingleAsync(
            file.OpenReadStream(), storage, env, Request, "receipts", maxWidth: 1200, maxHeight: 1600, quality: 80);

        MonzoTransactionDto? result = await monzo.SetReceiptUrlAsync(id, receiptUrl);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary()
    {
        MonzoSummaryDto summary = await monzo.GetSummaryAsync();
        return Ok(new
        {
            totalIncome = summary.TotalIncome,
            totalExpenses = summary.TotalExpenses,
            totalProfit = summary.TotalProfit,
            transactionCount = summary.TransactionCount,
            taggedCount = summary.TaggedCount,
            untaggedCount = summary.UntaggedCount,
            byMonth = summary.ByMonth.Select(m => new
            {
                month = m.Month,
                income = m.Income,
                expenses = m.Expenses,
                profit = m.Profit,
                count = m.Count,
                byCategory = m.ByCategory.Select(c => new { category = c.Category, total = c.Total, count = c.Count }),
                byPlatform = m.ByPlatform.Select(p => new { platform = p.Platform, total = p.Total, count = p.Count }),
            }),
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] int? year, [FromQuery] int? month)
    {
        MonzoExportFile export = await monzo.ExportAsync(year, month);
        return File(export.Content, "text/csv", export.FileName);
    }
}
