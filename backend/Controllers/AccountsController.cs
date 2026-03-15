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

        // Overall metrics
        decimal totalRevenue = paidOrders.Sum(o => o.Total);
        int totalOrders = paidOrders.Count;
        int totalItemsSold = paidOrders.SelectMany(o => o.Items).Sum(i => i.Quantity);
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

        decimal totalProfit = totalRevenue - totalCost;
        decimal marginPercent = totalRevenue > 0 ? Math.Round(totalProfit / totalRevenue * 100, 1) : 0;

        // Revenue by month
        var revenueByMonth = paidOrders
            .GroupBy(o => new { o.CreatedAtUtc.Year, o.CreatedAtUtc.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g =>
            {
                decimal monthRevenue = g.Sum(o => o.Total);
                decimal monthCost = 0;
                foreach (OrderItem item in g.SelectMany(o => o.Items))
                {
                    if (productLookup.TryGetValue(item.ProductId, out Product? product))
                    {
                        monthCost += product.CostPrice * item.Quantity;
                    }
                }
                return new
                {
                    month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    revenue = monthRevenue,
                    cost = monthCost,
                    profit = monthRevenue - monthCost,
                    orders = g.Count(),
                    itemsSold = g.SelectMany(o => o.Items).Sum(i => i.Quantity)
                };
            })
            .ToList();

        // Revenue by category (with profit)
        var revenueByCategory = paidOrders
            .SelectMany(o => o.Items)
            .GroupBy(i => productLookup.TryGetValue(i.ProductId, out Product? p) ? p.Category : "Unknown")
            .Select(g =>
            {
                decimal rev = g.Sum(i => i.UnitPrice * i.Quantity);
                decimal cost = g.Sum(i =>
                    productLookup.TryGetValue(i.ProductId, out Product? p) ? p.CostPrice * i.Quantity : 0);
                return new
                {
                    category = g.Key,
                    revenue = rev,
                    cost,
                    profit = rev - cost,
                    itemsSold = g.Sum(i => i.Quantity)
                };
            })
            .OrderByDescending(x => x.revenue)
            .ToList();

        // Revenue by era (with profit)
        var revenueByEra = paidOrders
            .SelectMany(o => o.Items)
            .GroupBy(i => productLookup.TryGetValue(i.ProductId, out Product? p) ? p.Era : "Unknown")
            .Select(g =>
            {
                decimal rev = g.Sum(i => i.UnitPrice * i.Quantity);
                decimal cost = g.Sum(i =>
                    productLookup.TryGetValue(i.ProductId, out Product? p) ? p.CostPrice * i.Quantity : 0);
                return new
                {
                    era = g.Key,
                    revenue = rev,
                    cost,
                    profit = rev - cost,
                    itemsSold = g.Sum(i => i.Quantity)
                };
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
}
