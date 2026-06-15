namespace Eden_Relics_BE.Services;

public interface IAccountsService
{
    Task<AccountsSummaryDto> GetSummaryAsync();
}

public record AccountsSummaryDto(
    decimal TotalRevenue,
    decimal TotalCost,
    decimal TotalProfit,
    decimal MarginPercent,
    int TotalOrders,
    int TotalItemsSold,
    decimal AverageOrderValue,
    List<MonthRevenueDto> RevenueByMonth,
    List<CategoryRevenueDto> RevenueByCategory,
    List<EraRevenueDto> RevenueByEra,
    InventorySummaryDto Inventory,
    List<OrderStatusCountDto> OrdersByStatus);

public record MonthRevenueDto(string Month, decimal Revenue, decimal Cost, decimal Profit, int Orders, int ItemsSold);
public record CategoryRevenueDto(string Category, decimal Revenue, decimal Cost, decimal Profit, int ItemsSold);
public record EraRevenueDto(string Era, decimal Revenue, decimal Cost, decimal Profit, int ItemsSold);
public record InventorySummaryDto(int InStock, int Stock, int OutOfStock, decimal RetailValue, decimal CostValue);
public record OrderStatusCountDto(string Status, int Count);
