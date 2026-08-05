using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// The one-of-one catalogue has no reservation during checkout: availability is checked when
/// the Stripe session is created, so two buyers can both pass that check and both pay. Nothing
/// used to notice — the webhook just set Sold again and both orders became Paid.
/// </summary>
public class DoubleSaleDetectionTests
{
    private static Product Piece(string name) => new()
    {
        Name = name,
        Sku = $"ER-{name.GetHashCode():X4}",
        Description = "d",
        Slug = name.ToLowerInvariant(),
        Era = "1970s",
        Category = "70s",
        Size = "10",
        Condition = "good",
        ImageUrl = "https://example.com/i.jpg",
    };

    [Fact]
    public void FlagsAPieceThatWasAlreadySoldWhenThePaymentLanded()
    {
        Product contested = Piece("Contested Dress");

        List<Product> conflicts = OrderService.DetectDoubleSales(
            firstDelivery: true,
            [(contested, ProductStatus.Sold)]);

        Assert.Same(contested, Assert.Single(conflicts));
    }

    [Fact]
    public void SaysNothingWhenThePieceWasStillLive()
    {
        List<Product> conflicts = OrderService.DetectDoubleSales(
            firstDelivery: true,
            [(Piece("Ordinary Dress"), ProductStatus.Live)]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void WebhookRetryOnTheSameOrderIsNotAConflict()
    {
        // Stripe re-delivers events. On the second delivery this order is already Paid and its
        // piece already Sold — by itself. Alerting here would fire on every retry.
        List<Product> conflicts = OrderService.DetectDoubleSales(
            firstDelivery: false,
            [(Piece("Retried Dress"), ProductStatus.Sold)]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void FlagsOnlyTheContestedPiecesInAMixedOrder()
    {
        Product contested = Piece("Contested");
        Product fine = Piece("Fine");

        List<Product> conflicts = OrderService.DetectDoubleSales(
            firstDelivery: true,
            [(fine, ProductStatus.Live), (contested, ProductStatus.Sold)]);

        Assert.Same(contested, Assert.Single(conflicts));
    }

    [Fact]
    public void APieceHeldBackInStockIsNotAConflict()
    {
        // Stock (not yet listed) is an odd thing to buy, but it is not a double sale.
        List<Product> conflicts = OrderService.DetectDoubleSales(
            firstDelivery: true,
            [(Piece("Held Back"), ProductStatus.Stock)]);

        Assert.Empty(conflicts);
    }
}
