using System.Text;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class FinanceService(
    IRepository<Transaction> transactions,
    IRepository<Order> orders,
    IRepository<Product> products) : IFinanceService
{
    public async Task<List<TransactionDto>> GetAllAsync(int? year, int? month)
    {
        IQueryable<Transaction> query = transactions.Query().OrderByDescending(t => t.Date);

        if (year.HasValue)
        {
            query = query.Where(t => t.Date.Year == year.Value);
        }
        if (month.HasValue)
        {
            query = query.Where(t => t.Date.Month == month.Value);
        }

        return await query.Select(t => ToDto(t)).ToListAsync();
    }

    public async Task<TransactionDto> CreateAsync(CreateTransactionDto dto)
    {
        Transaction transaction = new()
        {
            Date = dto.Date,
            Description = dto.Description,
            Amount = dto.Amount,
            Category = dto.Category,
            Platform = dto.Platform,
            Reference = dto.Reference,
            Notes = dto.Notes,
        };

        await transactions.AddAsync(transaction);
        return ToDto(transaction);
    }

    public async Task<TransactionDto?> UpdateAsync(Guid id, UpdateTransactionDto dto)
    {
        Transaction? transaction = await transactions.GetByIdAsync(id);
        if (transaction is null)
        {
            return null;
        }

        if (dto.Date.HasValue) { transaction.Date = dto.Date.Value; }
        if (dto.Description is not null) { transaction.Description = dto.Description; }
        if (dto.Amount.HasValue) { transaction.Amount = dto.Amount.Value; }
        if (dto.Category is not null) { transaction.Category = dto.Category; }
        if (dto.Platform is not null) { transaction.Platform = dto.Platform == "" ? null : dto.Platform; }
        if (dto.Reference is not null) { transaction.Reference = dto.Reference == "" ? null : dto.Reference; }
        if (dto.ReceiptUrl is not null) { transaction.ReceiptUrl = dto.ReceiptUrl == "" ? null : dto.ReceiptUrl; }
        if (dto.Notes is not null) { transaction.Notes = dto.Notes == "" ? null : dto.Notes; }

        await transactions.UpdateAsync(transaction);
        return ToDto(transaction);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        Transaction? transaction = await transactions.GetByIdAsync(id);
        if (transaction is null)
        {
            return false;
        }

        await transactions.DeleteAsync(id);
        return true;
    }

    /// One-shot backfill: ensures every known sale has a matching ledger transaction.
    /// Two paths, both idempotent:
    ///   1. Paid orders (Stripe checkout) — Reference = order.Id
    ///   2. Products marked Status=Sold but without a corresponding Order
    ///      (admin manually flipped them sold, sold offsite, etc.) — Reference = product.Id
    /// Safe to re-run.
    public async Task<BackfillSalesResultDto> BackfillSalesAsync()
    {
        int createdFromOrders = 0;
        int createdFromProducts = 0;
        int createdCogs = 0;
        List<Transaction> toCreate = [];

        // Path 1: paid orders → ledger
        List<Order> paidOrders = await orders.Query()
            .Include(o => o.Items)
            .Where(o => o.Status == "Paid")
            .ToListAsync();

        HashSet<Guid> productIdsCoveredByOrders = paidOrders
            .SelectMany(o => o.Items.Select(i => i.ProductId))
            .ToHashSet();

        foreach (Order order in paidOrders)
        {
            string orderRef = order.Id.ToString();
            bool exists = await transactions.Query().AnyAsync(t => t.Reference == orderRef);
            if (exists) { continue; }

            string description = order.Items.Count == 1
                ? $"Sale: {order.Items[0].ProductName}"
                : $"Sale: {order.Items.Count} items";

            toCreate.Add(new Transaction
            {
                Date = order.UpdatedAtUtc,
                Description = description,
                Amount = order.Total,
                Category = "Sales",
                Platform = "Website",
                Reference = orderRef,
            });
            createdFromOrders++;
        }

        // Path 2: products marked Sold without a matching Order → ledger
        // Skip any product that's already covered by an order-based transaction
        // (above) to avoid double-counting the same sale.
        List<Product> soldProducts = await products.Query()
            .Where(p => p.Status == ProductStatus.Sold)
            .ToListAsync();

        foreach (Product product in soldProducts)
        {
            if (productIdsCoveredByOrders.Contains(product.Id)) { continue; }

            string productRef = product.Id.ToString();
            bool exists = await transactions.Query().AnyAsync(t => t.Reference == productRef);
            if (exists) { continue; }

            decimal amount = product.SalePrice ?? product.Price;

            toCreate.Add(new Transaction
            {
                Date = product.UpdatedAtUtc,
                Description = $"Sale: {product.Name}",
                Amount = amount,
                Category = "Sales",
                // Platform unknown for the product-flip path — admin can fill in afterwards.
                Platform = null,
                Reference = productRef,
            });
            createdFromProducts++;
        }

        // Path 3: cost of goods sold → ledger. Every sold dress contributes an
        // expense equal to its cost price, keyed by product so it's idempotent and
        // independent of which income path recorded the sale. Dated at the sale
        // (UpdatedAtUtc) so income and COGS land in the same month for profit calc.
        foreach (Product product in soldProducts)
        {
            if (product.CostPrice <= 0) { continue; }

            string cogsRef = $"cogs:{product.Id}";
            bool exists = await transactions.Query().AnyAsync(t => t.Reference == cogsRef);
            if (exists) { continue; }

            toCreate.Add(new Transaction
            {
                Date = product.UpdatedAtUtc,
                Description = $"Cost of goods: {product.Name}",
                Amount = -product.CostPrice,
                Category = "Stock",
                Reference = cogsRef,
            });
            createdCogs++;
        }

        int totalCreated = createdFromOrders + createdFromProducts + createdCogs;
        if (totalCreated > 0)
        {
            await transactions.AddRangeAsync(toCreate);
        }

        return new BackfillSalesResultDto(
            totalCreated,
            paidOrders.Count,
            soldProducts.Count,
            new BackfillBreakdownDto(createdFromOrders, createdFromProducts, createdCogs));
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync()
    {
        List<Transaction> all = await transactions.Query().ToListAsync();

        List<FinanceMonthDto> byMonth = all
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g =>
            {
                decimal income = g.Where(t => t.Amount > 0).Sum(t => t.Amount);
                decimal expenses = g.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
                return new FinanceMonthDto(
                    $"{g.Key.Year}-{g.Key.Month:D2}",
                    income,
                    expenses,
                    income - expenses,
                    g.Count(),
                    g.GroupBy(t => t.Category)
                        .Select(c => new FinanceCategoryDto(c.Key, c.Sum(t => t.Amount), c.Count()))
                        .OrderByDescending(c => Math.Abs(c.Total))
                        .ToList(),
                    g.Where(t => t.Platform is not null)
                        .GroupBy(t => t.Platform!)
                        .Select(p => new FinancePlatformDto(p.Key, p.Sum(t => t.Amount), p.Count()))
                        .OrderByDescending(p => Math.Abs(p.Total))
                        .ToList());
            })
            .ToList();

        decimal totalIncome = all.Where(t => t.Amount > 0).Sum(t => t.Amount);
        decimal totalExpenses = all.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));

        return new FinanceSummaryDto(
            Math.Round(totalIncome, 2),
            Math.Round(totalExpenses, 2),
            Math.Round(totalIncome - totalExpenses, 2),
            all.Count,
            byMonth);
    }

    /// Rolling 13-month profit-and-loss snapshot for the admin accounting dashboard.
    /// Built entirely from the Transactions ledger: revenue is positive amounts, expenses are
    /// the magnitude of negative amounts, split out by category. The rolling-12 totals skip the
    /// oldest (13th) month so a part-month at the edge doesn't distort the headline figures.
    public async Task<AccountingSnapshot> GetPnlAsync()
    {
        DateTime now = DateTime.UtcNow;
        DateTime windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-12);

        List<Transaction> rows = await transactions.Query()
            .Where(t => t.Date >= windowStart)
            .ToListAsync();

        // Seed all 13 months so gaps render as zero rows rather than disappearing.
        List<MonthlyPnlRow> months = [];
        Dictionary<(int Year, int Month), List<Transaction>> byMonth = rows
            .GroupBy(t => (t.Date.Year, t.Date.Month))
            .ToDictionary(g => g.Key, g => g.ToList());

        for (int i = -12; i <= 0; i++)
        {
            DateTime m = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(i);
            List<Transaction> bucket = byMonth.GetValueOrDefault((m.Year, m.Month)) ?? [];
            decimal revenue = bucket.Where(t => t.Amount > 0).Sum(t => t.Amount);
            decimal expenses = bucket.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
            months.Add(new MonthlyPnlRow(m.Year, m.Month, revenue, expenses, revenue - expenses));
        }

        // Rolling 12 = the most recent 12 of the 13 (skip the oldest).
        List<MonthlyPnlRow> last12 = months.Skip(1).ToList();
        decimal totalRevenue = last12.Sum(m => m.Revenue);
        decimal totalExpenses = last12.Sum(m => m.Expenses);

        List<ExpenseCategoryTotal> split = rows
            .Where(t => t.Amount < 0)
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Other" : t.Category)
            .Select(g => new ExpenseCategoryTotal(g.Key, g.Sum(t => Math.Abs(t.Amount))))
            .OrderByDescending(t => t.Amount)
            .ToList();

        List<TransactionDto> recent = rows
            .OrderByDescending(t => t.Date)
            .Take(25)
            .Select(ToDto)
            .ToList();

        return new AccountingSnapshot(
            Currency: "GBP",
            Months: months,
            TotalRevenue: totalRevenue,
            TotalExpenses: totalExpenses,
            TotalNet: totalRevenue - totalExpenses,
            ExpenseSplit: split,
            RecentTransactions: recent,
            GeneratedAt: now);
    }

    public async Task<FinanceExportFile> ExportAsync(int? year, int? month)
    {
        IQueryable<Transaction> query = transactions.Query().OrderByDescending(t => t.Date);

        if (year.HasValue)
        {
            query = query.Where(t => t.Date.Year == year.Value);
        }
        if (month.HasValue)
        {
            query = query.Where(t => t.Date.Month == month.Value);
        }

        List<Transaction> rows = await query.ToListAsync();

        StringBuilder csv = new();
        csv.AppendLine("Date,Description,Amount,Category,Platform,Reference,Notes");
        foreach (Transaction t in rows)
        {
            csv.AppendLine(
                $"{t.Date:yyyy-MM-dd}," +
                $"\"{Escape(t.Description)}\"," +
                $"{t.Amount}," +
                $"\"{Escape(t.Category)}\"," +
                $"\"{Escape(t.Platform ?? "")}\"," +
                $"\"{Escape(t.Reference ?? "")}\"," +
                $"\"{Escape(t.Notes ?? "")}\"");
        }

        string fileName = year.HasValue && month.HasValue
            ? $"transactions-{year}-{month:D2}.csv"
            : year.HasValue
                ? $"transactions-{year}.csv"
                : "transactions-all.csv";

        return new FinanceExportFile(Encoding.UTF8.GetBytes(csv.ToString()), fileName);
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

    private static TransactionDto ToDto(Transaction t) => new(
        t.Id, t.Date, t.Description, t.Amount, t.Category,
        t.Platform, t.Reference, t.ReceiptUrl, t.Notes, t.CreatedAtUtc);
}
