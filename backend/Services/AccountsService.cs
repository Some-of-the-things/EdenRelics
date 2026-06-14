using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class AccountsService(
    IRepository<Order> orders,
    IRepository<Product> products,
    IRepository<OffsiteSale> offsiteSales) : IAccountsService
{
    private static readonly HashSet<string> PaidStatuses = ["Paid", "Processing", "Shipped", "Delivered"];

    public async Task<AccountsSummaryDto> GetSummaryAsync()
    {
        List<Order> paidOrders = await orders.Query()
            .Include(o => o.Items)
            .Where(o => PaidStatuses.Contains(o.Status))
            .ToListAsync();

        List<Product> allProducts = await products.Query().ToListAsync();
        Dictionary<Guid, Product> productLookup = allProducts.ToDictionary(p => p.Id);

        List<OffsiteSale> sales = await offsiteSales.Query().ToListAsync();

        // Overall metrics — combine on-site orders with offsite sales
        decimal onsiteRevenue = paidOrders.Sum(o => o.Total);
        decimal offsiteRevenue = sales.Sum(s => s.SalePrice);
        decimal totalRevenue = onsiteRevenue + offsiteRevenue;

        int totalOrders = paidOrders.Count + sales.Count;
        int totalItemsSold = paidOrders.SelectMany(o => o.Items).Sum(i => i.Quantity) + sales.Count;
        decimal averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

        // Cost & profit - look up the current cost price for each sold item
        decimal totalCost = 0;
        foreach (OrderItem item in paidOrders.SelectMany(o => o.Items))
        {
            if (productLookup.TryGetValue(item.ProductId, out Product? product))
            {
                totalCost += product.CostPrice * item.Quantity;
            }
        }
        totalCost += sales.Sum(s => s.CostPrice);

        decimal totalProfit = totalRevenue - totalCost;
        decimal marginPercent = totalRevenue > 0 ? Math.Round(totalProfit / totalRevenue * 100, 1) : 0;

        // Revenue by month — merge on-site and offsite buckets
        Dictionary<string, MonthlyTotals> monthlyTotals = [];

        foreach (Order order in paidOrders)
        {
            string key = $"{order.CreatedAtUtc.Year}-{order.CreatedAtUtc.Month:D2}";
            MonthlyTotals bucket = monthlyTotals.TryGetValue(key, out MonthlyTotals? m) ? m : new MonthlyTotals();
            bucket.Revenue += order.Total;
            bucket.Orders += 1;
            foreach (OrderItem item in order.Items)
            {
                bucket.ItemsSold += item.Quantity;
                if (productLookup.TryGetValue(item.ProductId, out Product? product))
                {
                    bucket.Cost += product.CostPrice * item.Quantity;
                }
            }
            monthlyTotals[key] = bucket;
        }

        foreach (OffsiteSale sale in sales)
        {
            string key = $"{sale.SaleDateUtc.Year}-{sale.SaleDateUtc.Month:D2}";
            MonthlyTotals bucket = monthlyTotals.TryGetValue(key, out MonthlyTotals? m) ? m : new MonthlyTotals();
            bucket.Revenue += sale.SalePrice;
            bucket.Cost += sale.CostPrice;
            bucket.Orders += 1;
            bucket.ItemsSold += 1;
            monthlyTotals[key] = bucket;
        }

        List<MonthRevenueDto> revenueByMonth = monthlyTotals
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new MonthRevenueDto(
                kv.Key, kv.Value.Revenue, kv.Value.Cost, kv.Value.Revenue - kv.Value.Cost, kv.Value.Orders, kv.Value.ItemsSold))
            .ToList();

        // Revenue by category — merge on-site and offsite
        Dictionary<string, CategoryTotals> categoryTotals = [];
        foreach (OrderItem item in paidOrders.SelectMany(o => o.Items))
        {
            string key = productLookup.TryGetValue(item.ProductId, out Product? p) ? p.Category : "Unknown";
            CategoryTotals bucket = categoryTotals.TryGetValue(key, out CategoryTotals? c) ? c : new CategoryTotals();
            bucket.Revenue += item.UnitPrice * item.Quantity;
            bucket.Cost += (productLookup.TryGetValue(item.ProductId, out Product? p2) ? p2.CostPrice : 0) * item.Quantity;
            bucket.ItemsSold += item.Quantity;
            categoryTotals[key] = bucket;
        }
        foreach (OffsiteSale sale in sales)
        {
            string key = string.IsNullOrWhiteSpace(sale.Category) ? "Unknown" : sale.Category;
            CategoryTotals bucket = categoryTotals.TryGetValue(key, out CategoryTotals? c) ? c : new CategoryTotals();
            bucket.Revenue += sale.SalePrice;
            bucket.Cost += sale.CostPrice;
            bucket.ItemsSold += 1;
            categoryTotals[key] = bucket;
        }
        List<CategoryRevenueDto> revenueByCategory = categoryTotals
            .Select(kv => new CategoryRevenueDto(
                kv.Key, kv.Value.Revenue, kv.Value.Cost, kv.Value.Revenue - kv.Value.Cost, kv.Value.ItemsSold))
            .OrderByDescending(x => x.Revenue)
            .ToList();

        // Revenue by era — merge on-site and offsite
        Dictionary<string, CategoryTotals> eraTotals = [];
        foreach (OrderItem item in paidOrders.SelectMany(o => o.Items))
        {
            string key = productLookup.TryGetValue(item.ProductId, out Product? p) ? p.Era : "Unknown";
            CategoryTotals bucket = eraTotals.TryGetValue(key, out CategoryTotals? c) ? c : new CategoryTotals();
            bucket.Revenue += item.UnitPrice * item.Quantity;
            bucket.Cost += (productLookup.TryGetValue(item.ProductId, out Product? p2) ? p2.CostPrice : 0) * item.Quantity;
            bucket.ItemsSold += item.Quantity;
            eraTotals[key] = bucket;
        }
        foreach (OffsiteSale sale in sales)
        {
            string key = string.IsNullOrWhiteSpace(sale.Era) ? "Unknown" : sale.Era;
            CategoryTotals bucket = eraTotals.TryGetValue(key, out CategoryTotals? c) ? c : new CategoryTotals();
            bucket.Revenue += sale.SalePrice;
            bucket.Cost += sale.CostPrice;
            bucket.ItemsSold += 1;
            eraTotals[key] = bucket;
        }
        List<EraRevenueDto> revenueByEra = eraTotals
            .Select(kv => new EraRevenueDto(
                kv.Key, kv.Value.Revenue, kv.Value.Cost, kv.Value.Revenue - kv.Value.Cost, kv.Value.ItemsSold))
            .OrderByDescending(x => x.Revenue)
            .ToList();

        // Inventory summary — Stock + Live count as "owned" inventory you haven't sold yet.
        int totalLive = allProducts.Count(p => p.Status == ProductStatus.Live);
        int totalStock = allProducts.Count(p => p.Status == ProductStatus.Stock);
        int totalSold = allProducts.Count(p => p.Status == ProductStatus.Sold);
        decimal inventoryRetailValue = allProducts.Where(p => p.Status != ProductStatus.Sold).Sum(p => p.Price);
        decimal inventoryCostValue = allProducts.Where(p => p.Status != ProductStatus.Sold).Sum(p => p.CostPrice);

        // Orders by status (all orders, not just paid)
        List<Order> allOrders = await orders.Query().ToListAsync();
        List<OrderStatusCountDto> ordersByStatus = allOrders
            .GroupBy(o => o.Status)
            .Select(g => new OrderStatusCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        return new AccountsSummaryDto(
            Math.Round(totalRevenue, 2),
            Math.Round(totalCost, 2),
            Math.Round(totalProfit, 2),
            marginPercent,
            totalOrders,
            totalItemsSold,
            Math.Round(averageOrderValue, 2),
            revenueByMonth,
            revenueByCategory,
            revenueByEra,
            new InventorySummaryDto(
                totalLive,
                totalStock,
                totalSold,
                Math.Round(inventoryRetailValue, 2),
                Math.Round(inventoryCostValue, 2)),
            ordersByStatus);
    }

    private sealed class MonthlyTotals
    {
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public int Orders { get; set; }
        public int ItemsSold { get; set; }
    }

    private sealed class CategoryTotals
    {
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public int ItemsSold { get; set; }
    }
}
