namespace Eden_Relics_BE.DTOs;

/// <summary>One month's profit-and-loss slice, derived from the Transactions ledger. Revenue is
/// the sum of positive amounts; Expenses the sum of the magnitudes of negative amounts.</summary>
public record MonthlyPnlRow(
    int Year,
    int Month,
    decimal Revenue,
    decimal Expenses,
    decimal Net);

/// <summary>Per-category split of expense (negative-amount) transactions for the window.</summary>
public record ExpenseCategoryTotal(string Category, decimal Amount);

/// <summary>
/// Rolling monthly P&amp;L snapshot for the admin accounting page. Covers the last 13 months so
/// the rolling-12 total has a comparator alongside. Built entirely from the Transactions ledger.
/// </summary>
public record AccountingSnapshot(
    string Currency,
    List<MonthlyPnlRow> Months,
    decimal TotalRevenue,
    decimal TotalExpenses,
    decimal TotalNet,
    List<ExpenseCategoryTotal> ExpenseSplit,
    List<TransactionDto> RecentTransactions,
    DateTime GeneratedAt);
