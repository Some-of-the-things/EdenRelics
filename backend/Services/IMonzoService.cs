using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface IMonzoService
{
    Task<MonzoStatusDto> GetStatusAsync();
    Task<MonzoConnectResult> ConnectAsync();
    Task<MonzoCallbackResult> CompleteCallbackAsync(MonzoCallbackDto dto);
    Task<MonzoVerifyResult> VerifyAsync();
    Task DisconnectAsync();
    Task<MonzoDebugDto?> DebugTransactionsAsync();

    /// <summary>Lists Monzo accounts. Null if Monzo is not connected.</summary>
    Task<List<MonzoAccountResponse>?> GetAccountsAsync();

    /// <summary>Lists pots. Null if Monzo is not connected.</summary>
    Task<List<MonzoPotDto>?> GetPotsAsync();

    Task<MonzoBalanceResult> GetBalanceAsync();

    /// <summary>Runs an incremental sync. Null if Monzo is not connected. Throws on Monzo API failure.</summary>
    Task<MonzoSyncResult?> SyncAsync();

    Task<List<MonzoTransactionDto>> GetTransactionsAsync(int? year, int? month);
    Task<MonzoTransactionDto?> AnnotateAsync(Guid id, MonzoAnnotateDto dto);

    /// <summary>Persists a receipt URL against a transaction. Null if the transaction is not found.</summary>
    Task<MonzoTransactionDto?> SetReceiptUrlAsync(Guid id, string receiptUrl);

    Task<MonzoSummaryDto> GetSummaryAsync();
    Task<MonzoExportFile> ExportAsync(int? year, int? month);

    /// <summary>Entry point for the hosted background worker: ensure token, then sync if connected.</summary>
    Task RunScheduledSyncAsync();
}

public record MonzoStatusDto(bool Connected, bool PendingApproval, string? AccountId);

public enum MonzoConnectOutcome { NotConfigured, Success }
public record MonzoConnectResult(MonzoConnectOutcome Outcome, string? Url, string? State);

public enum MonzoCallbackOutcome { NotConfigured, InvalidState, ExchangeFailed, Success }
public record MonzoCallbackResult(MonzoCallbackOutcome Outcome, string? ExchangeError);

public enum MonzoVerifyOutcome { NoToken, TokenExpired, AwaitingApproval, Verified }
public record MonzoVerifyResult(MonzoVerifyOutcome Outcome, string? AccountId);

public enum MonzoBalanceOutcome { NotConnected, FetchFailed, Success }
public record MonzoBalanceResult(MonzoBalanceOutcome Outcome, MonzoBalanceDto? Balance);

public record MonzoSyncResult(int Fetched, int Added, string AccountId, DateTime? Since);

public record MonzoDebugDto(
    string AccountId,
    DateTime TokenExpiresAt,
    int NoFilterCount,
    int RecentCount,
    int LongerCount,
    MonzoTransactionResponse? SampleTransaction);

public record MonzoSummaryDto(
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal TotalProfit,
    int TransactionCount,
    int TaggedCount,
    int UntaggedCount,
    List<MonzoMonthSummaryDto> ByMonth);

public record MonzoMonthSummaryDto(
    string Month,
    decimal Income,
    decimal Expenses,
    decimal Profit,
    int Count,
    List<MonzoCategorySummaryDto> ByCategory,
    List<MonzoPlatformSummaryDto> ByPlatform);

public record MonzoCategorySummaryDto(string Category, decimal Total, int Count);
public record MonzoPlatformSummaryDto(string Platform, decimal Total, int Count);

public record MonzoExportFile(byte[] Content, string FileName);
