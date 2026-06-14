using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface IFinanceService
{
    Task<List<TransactionDto>> GetAllAsync(int? year, int? month);
    Task<TransactionDto> CreateAsync(CreateTransactionDto dto);
    Task<TransactionDto?> UpdateAsync(Guid id, UpdateTransactionDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<BackfillSalesResultDto> BackfillSalesAsync();
    Task<FinanceSummaryDto> GetSummaryAsync();
    Task<AccountingSnapshot> GetPnlAsync();
    Task<FinanceExportFile> ExportAsync(int? year, int? month);
}

public record FinanceSummaryDto(
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal TotalProfit,
    int TransactionCount,
    List<FinanceMonthDto> ByMonth);

public record FinanceMonthDto(
    string Month,
    decimal Income,
    decimal Expenses,
    decimal Profit,
    int Count,
    List<FinanceCategoryDto> ByCategory,
    List<FinancePlatformDto> ByPlatform);

public record FinanceCategoryDto(string Category, decimal Total, int Count);
public record FinancePlatformDto(string Platform, decimal Total, int Count);

public record BackfillSalesResultDto(int Backfilled, int TotalPaid, int TotalSoldProducts, BackfillBreakdownDto Breakdown);
public record BackfillBreakdownDto(int FromOrders, int FromProducts, int Cogs);

public record FinanceExportFile(byte[] Content, string FileName);
