using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AccountsController(EdenRelicsDbContext context) : ControllerBase
{
    private static readonly HashSet<string> PaidStatuses = ["Paid", "Processing", "Shipped", "Delivered"];

    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary()
    {
        List<Order> paidOrders = await context.Orders
            .Include(o => o.Items)
            .Where(o => PaidStatuses.Contains(o.Status))
            .ToListAsync();

        List<Product> products = await context.Products.ToListAsync();
        Dictionary<Guid, Product> productLookup = products.ToDictionary(p => p.Id);

        List<OffsiteSale> offsiteSales = await context.OffsiteSales.ToListAsync();

        // Overall metrics — combine on-site orders with offsite sales
        decimal onsiteRevenue = paidOrders.Sum(o => o.Total);
        decimal offsiteRevenue = offsiteSales.Sum(s => s.SalePrice);
        decimal totalRevenue = onsiteRevenue + offsiteRevenue;

        int totalOrders = paidOrders.Count + offsiteSales.Count;
        int totalItemsSold = paidOrders.SelectMany(o => o.Items).Sum(i => i.Quantity) + offsiteSales.Count;
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
        totalCost += offsiteSales.Sum(s => s.CostPrice);

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

        foreach (OffsiteSale sale in offsiteSales)
        {
            string key = $"{sale.SaleDateUtc.Year}-{sale.SaleDateUtc.Month:D2}";
            MonthlyTotals bucket = monthlyTotals.TryGetValue(key, out MonthlyTotals? m) ? m : new MonthlyTotals();
            bucket.Revenue += sale.SalePrice;
            bucket.Cost += sale.CostPrice;
            bucket.Orders += 1;
            bucket.ItemsSold += 1;
            monthlyTotals[key] = bucket;
        }

        var revenueByMonth = monthlyTotals
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new
            {
                month = kv.Key,
                revenue = kv.Value.Revenue,
                cost = kv.Value.Cost,
                profit = kv.Value.Revenue - kv.Value.Cost,
                orders = kv.Value.Orders,
                itemsSold = kv.Value.ItemsSold
            })
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
        foreach (OffsiteSale sale in offsiteSales)
        {
            string key = string.IsNullOrWhiteSpace(sale.Category) ? "Unknown" : sale.Category;
            CategoryTotals bucket = categoryTotals.TryGetValue(key, out CategoryTotals? c) ? c : new CategoryTotals();
            bucket.Revenue += sale.SalePrice;
            bucket.Cost += sale.CostPrice;
            bucket.ItemsSold += 1;
            categoryTotals[key] = bucket;
        }
        var revenueByCategory = categoryTotals
            .Select(kv => new
            {
                category = kv.Key,
                revenue = kv.Value.Revenue,
                cost = kv.Value.Cost,
                profit = kv.Value.Revenue - kv.Value.Cost,
                itemsSold = kv.Value.ItemsSold
            })
            .OrderByDescending(x => x.revenue)
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
        foreach (OffsiteSale sale in offsiteSales)
        {
            string key = string.IsNullOrWhiteSpace(sale.Era) ? "Unknown" : sale.Era;
            CategoryTotals bucket = eraTotals.TryGetValue(key, out CategoryTotals? c) ? c : new CategoryTotals();
            bucket.Revenue += sale.SalePrice;
            bucket.Cost += sale.CostPrice;
            bucket.ItemsSold += 1;
            eraTotals[key] = bucket;
        }
        var revenueByEra = eraTotals
            .Select(kv => new
            {
                era = kv.Key,
                revenue = kv.Value.Revenue,
                cost = kv.Value.Cost,
                profit = kv.Value.Revenue - kv.Value.Cost,
                itemsSold = kv.Value.ItemsSold
            })
            .OrderByDescending(x => x.revenue)
            .ToList();

        // Inventory summary
        int totalInStock = products.Count(p => p.InStock);
        int totalOutOfStock = products.Count(p => !p.InStock);
        decimal inventoryRetailValue = products.Where(p => p.InStock).Sum(p => p.Price);
        decimal inventoryCostValue = products.Where(p => p.InStock).Sum(p => p.CostPrice);

        // Orders by status (all orders, not just paid)
        List<Order> allOrders = await context.Orders.ToListAsync();
        var ordersByStatus = allOrders
            .GroupBy(o => o.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        return Ok(new
        {
            totalRevenue = Math.Round(totalRevenue, 2),
            totalCost = Math.Round(totalCost, 2),
            totalProfit = Math.Round(totalProfit, 2),
            marginPercent,
            totalOrders,
            totalItemsSold,
            averageOrderValue = Math.Round(averageOrderValue, 2),
            revenueByMonth,
            revenueByCategory,
            revenueByEra,
            inventory = new
            {
                inStock = totalInStock,
                outOfStock = totalOutOfStock,
                retailValue = Math.Round(inventoryRetailValue, 2),
                costValue = Math.Round(inventoryCostValue, 2)
            },
            ordersByStatus
        });
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
