using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public interface ISalesLedgerService
{
    /// <summary>
    /// Records the sale of a product in the finance ledger, if it isn't recorded already.
    /// Call this from every route that can move a product to Sold.
    /// </summary>
    /// <param name="product">The product being sold. Its Status is expected to be Sold.</param>
    /// <param name="platform">Where it sold, when the caller knows. Null leaves it Unspecified.</param>
    /// <returns>True if a transaction was written, false if one already existed.</returns>
    Task<bool> RecordSaleAsync(Product product, string? platform);

    /// <summary>
    /// Brings an already-recorded sale's amount back in line with the product's price, for when
    /// the price is corrected after the sale was logged. No-op if the product isn't sold or has
    /// no sale row yet.
    /// </summary>
    /// <returns>True if an existing transaction was changed.</returns>
    Task<bool> SyncSaleAmountAsync(Product product);
}

/// <summary>
/// The one place a sale becomes money in the ledger.
/// </summary>
/// <remarks>
/// This used to be written inline in ProductsController, which meant it only ran for the route
/// that happened to contain it. MarketplaceService.MarkSoldAsync — the "Mark sold on..." control
/// on the crosslisting tab, the natural way to record a Vinted/Depop sale — set Status to Sold
/// and wrote nothing to the ledger, so those sales never counted as income (reported 2026-08-19).
///
/// Extracted so the two callers cannot drift again, and so a third route added later has an
/// obvious thing to call. Deliberately a service the callers invoke rather than a save
/// interceptor: an interceptor is for invariants that must never be bypassed (soft-delete), and
/// recording income is a decision a caller makes, not a property of writing the row.
/// </remarks>
public class SalesLedgerService(
    IRepository<Transaction> transactions,
    ILogger<SalesLedgerService> logger) : ISalesLedgerService
{
    public async Task<bool> RecordSaleAsync(Product product, string? platform)
    {
        try
        {
            // A product carries TWO ledger rows keyed on its id: the stock purchase written at
            // creation (Category "Stock") and the sale written here. Matching on Reference alone
            // finds the stock row and silently skips the sale, which is how every piece bought
            // with a cost price and a purchase date recorded its expense and never its income
            // (found 2026-08-17: three sold pieces, GBP 96 of income missing).
            string productRef = product.Id.ToString();
            bool exists = await transactions.Query()
                .AnyAsync(t => t.Reference == productRef && t.Category == TransactionCategories.Sales);
            if (exists)
            {
                return false;
            }

            await transactions.AddAsync(new Transaction
            {
                Date = DateTime.UtcNow,
                Description = $"Sale: {product.Name}",
                // What it sold for: the discounted price when one is set, the list price otherwise.
                Amount = product.SalePrice ?? product.Price,
                Category = TransactionCategories.Sales,
                // Set when the caller knows (the marketplace and Stripe paths do); null on the
                // plain status-dropdown edit, where the admin can fill it in afterwards.
                Platform = platform,
                Reference = productRef,
            });
            return true;
        }
        catch (Exception ex)
        {
            // Recording income must never block the sale itself being marked.
            logger.LogWarning(ex, "Failed to record sale transaction for product {ProductId}", product.Id);
            return false;
        }
    }

    public async Task<bool> SyncSaleAmountAsync(Product product)
    {
        if (product.Status != ProductStatus.Sold)
        {
            return false;
        }

        try
        {
            string productRef = product.Id.ToString();
            Transaction? sale = await transactions.Query()
                .FirstOrDefaultAsync(t => t.Reference == productRef && t.Category == TransactionCategories.Sales);
            if (sale is null)
            {
                return false;
            }

            decimal amount = product.SalePrice ?? product.Price;
            if (sale.Amount == amount)
            {
                return false;
            }

            // The product's price is treated as the source of truth here, so an amount typed
            // straight onto the transaction is overwritten if the product's price is edited
            // afterwards. That is the lesser evil: the alternative is a sale price that visibly
            // disagrees with the ledger and no way to tell which one the totals used.
            sale.Amount = amount;
            await transactions.UpdateAsync(sale);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync sale amount for product {ProductId}", product.Id);
            return false;
        }
    }
}
