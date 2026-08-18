using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using Product = Eden_Relics_BE.Data.Entities.Product;

namespace Eden_Relics_BE.Services;

public class OrderService(
    IRepository<Order> orders,
    IRepository<Product> products,
    IRepository<Transaction> transactions,
    IConfiguration configuration,
    IEmailService emailService,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<CreateOrderResult> CreateAsync(CreateOrderDto dto, Guid? userId)
    {
        if (userId is null && string.IsNullOrWhiteSpace(dto.GuestEmail))
        {
            return new CreateOrderResult(null, "Guest email is required for anonymous checkout.");
        }

        if (dto.Items is null || dto.Items.Count == 0)
        {
            return new CreateOrderResult(null, "Order must contain at least one item.");
        }

        List<Product> orderProducts = [];
        foreach (CreateOrderItemDto item in dto.Items)
        {
            // Every product is one-of-one, so quantity is always 1 and an item can't repeat.
            if (item.Quantity != 1)
            {
                return new CreateOrderResult(null, "Each item is one-of-a-kind; quantity must be 1.");
            }
            if (orderProducts.Any(p => p.Id == item.ProductId))
            {
                return new CreateOrderResult(null, "An item appears more than once in the order.");
            }
            Product? product = await products.GetByIdAsync(item.ProductId);
            if (product is null)
            {
                return new CreateOrderResult(null, $"Product {item.ProductId} not found.");
            }
            if (product.Status != ProductStatus.Live)
            {
                return new CreateOrderResult(null, $"Product {item.ProductId} is not available for purchase.");
            }
            orderProducts.Add(product);
        }

        // Determine shipping cost based on zone
        decimal shippingCost = ShippingZones.GetShippingCost(
            dto.ShippingMethod, dto.ShippingAddress?.Country);

        Order order = new()
        {
            UserId = userId,
            GuestEmail = userId is null ? dto.GuestEmail!.Trim().ToLowerInvariant() : null,
            ShippingMethod = dto.ShippingMethod ?? "standard",
            ShippingCost = shippingCost,
            ShipAddressLine1 = dto.ShippingAddress?.AddressLine1,
            ShipAddressLine2 = dto.ShippingAddress?.AddressLine2,
            ShipCity = dto.ShippingAddress?.City,
            ShipCounty = dto.ShippingAddress?.County,
            ShipPostcode = dto.ShippingAddress?.Postcode,
            ShipCountry = dto.ShippingAddress?.Country,
            BillAddressLine1 = dto.BillingAddress?.AddressLine1,
            BillAddressLine2 = dto.BillingAddress?.AddressLine2,
            BillCity = dto.BillingAddress?.City,
            BillCounty = dto.BillingAddress?.County,
            BillPostcode = dto.BillingAddress?.Postcode,
            BillCountry = dto.BillingAddress?.Country,
            Items = dto.Items.Select(item =>
            {
                Product product = orderProducts.First(p => p.Id == item.ProductId);
                return new OrderItem
                {
                    ProductId = product.Id,
                    SellerId = product.SellerId,
                    ProductName = product.Name,
                    UnitPrice = product.SalePrice ?? product.Price,
                    Quantity = item.Quantity
                };
            }).ToList()
        };

        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity) + shippingCost;

        await orders.AddAsync(order);

        // Create Stripe Checkout Session
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        string frontendUrl = configuration["Stripe:FrontendUrl"] ?? "http://localhost:4200";

        List<SessionLineItemOptions> lineItems = order.Items.Select(item => new SessionLineItemOptions
        {
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = "gbp",
                UnitAmountDecimal = item.UnitPrice * 100,
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = item.ProductName,
                },
            },
            Quantity = item.Quantity,
        }).ToList();

        if (shippingCost > 0)
        {
            lineItems.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "gbp",
                    UnitAmountDecimal = shippingCost * 100,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"Shipping ({order.ShippingMethod})",
                    },
                },
                Quantity = 1,
            });
        }

        SessionCreateOptions sessionOptions = new()
        {
            PaymentMethodTypes = ["card"],
            LineItems = lineItems,
            Mode = "payment",
            // Show the promotion-code field on Stripe Checkout so newsletter/discount
            // codes (e.g. WELCOME15) can be redeemed. Stripe recalculates the total
            // server-side from the coupon, so the amount charged stays authoritative.
            AllowPromotionCodes = true,
            SuccessUrl = $"{frontendUrl}/order-confirmation/{order.Id}?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{frontendUrl}/cart",
            Metadata = new Dictionary<string, string>
            {
                { "order_id", order.Id.ToString() }
            },
            ShippingAddressCollection = new SessionShippingAddressCollectionOptions
            {
                AllowedCountries = ["GB"],
            },
            ShippingOptions =
            [
                new SessionShippingOptionOptions
                {
                    ShippingRateData = new SessionShippingOptionShippingRateDataOptions
                    {
                        Type = "fixed_amount",
                        DisplayName = "Free standard delivery",
                        FixedAmount = new SessionShippingOptionShippingRateDataFixedAmountOptions
                        {
                            Amount = 0,
                            Currency = "gbp",
                        },
                        DeliveryEstimate = new SessionShippingOptionShippingRateDataDeliveryEstimateOptions
                        {
                            Minimum = new SessionShippingOptionShippingRateDataDeliveryEstimateMinimumOptions
                            {
                                Unit = "business_day",
                                Value = 3,
                            },
                            Maximum = new SessionShippingOptionShippingRateDataDeliveryEstimateMaximumOptions
                            {
                                Unit = "business_day",
                                Value = 5,
                            },
                        },
                    },
                },
            ],
        };

        if (userId is null)
        {
            sessionOptions.CustomerEmail = dto.GuestEmail!.Trim().ToLowerInvariant();
        }

        SessionService service = new();
        Session session = await service.CreateAsync(sessionOptions);

        order.StripeSessionId = session.Id;
        await orders.UpdateAsync(order);

        return new CreateOrderResult(new CheckoutResponseDto(order.Id, session.Url!), null);
    }

    public async Task<bool> HandleWebhookAsync(string json, string signature)
    {
        string endpointSecret = configuration["Stripe:WebhookSecret"]!;

        try
        {
            Event stripeEvent = EventUtility.ConstructEvent(json, signature, endpointSecret);

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                Session? session = stripeEvent.Data.Object as Session;
                if (session?.Metadata.TryGetValue("order_id", out string? orderIdStr) == true
                    && Guid.TryParse(orderIdStr, out Guid orderId))
                {
                    Order? order = await orders.Query()
                        .Include(o => o.Items)
                        .Include(o => o.User)
                        .FirstOrDefaultAsync(o => o.Id == orderId);
                    if (order is not null)
                    {
                        // Captured before we flip it: on a Stripe webhook RETRY this order is
                        // already Paid and its products are already Sold, which must not be
                        // mistaken for two customers buying the same piece.
                        bool firstDelivery = order.Status != "Paid";
                        order.Status = "Paid";

                        // Mark sold products and flag marketplace listings for removal.
                        // Statuses are captured BEFORE anything is mutated, so the
                        // double-sale check sees what was true when the payment landed.
                        List<Product> soldProducts = [];
                        List<(Product Product, ProductStatus StatusBeforePayment)> beforePayment = [];
                        foreach (OrderItem item in order.Items)
                        {
                            Product? product = await products.Query()
                                .Include(p => p.Listings)
                                .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                            if (product is not null)
                            {
                                beforePayment.Add((product, product.Status));
                                product.Status = ProductStatus.Sold;
                                foreach (ProductListing listing in product.Listings.Where(l => l.Status == "Active"))
                                {
                                    listing.Status = listing.Platform == "Website" ? "Sold" : "PendingRemoval";
                                }
                                soldProducts.Add(product);
                            }
                        }

                        List<Product> alreadySold = DetectDoubleSales(firstDelivery, beforePayment);
                        if (alreadySold.Count > 0)
                        {
                            await AlertDoubleSaleAsync(order, alreadySold);
                        }

                        // Mirror the sale into the finance ledger. Idempotent by order-id Reference
                        // so Stripe webhook retries don't create duplicates.
                        string orderRef = order.Id.ToString();
                        bool alreadyLogged = await transactions.Query().AnyAsync(t => t.Reference == orderRef);
                        bool isNewSale = !alreadyLogged;
                        List<Transaction> ledgerEntries = [];
                        if (isNewSale)
                        {
                            string description = order.Items.Count == 1
                                ? $"Sale: {order.Items[0].ProductName}"
                                : $"Sale: {order.Items.Count} items";
                            ledgerEntries.Add(new Transaction
                            {
                                Date = DateTime.UtcNow,
                                Description = description,
                                Amount = order.Total,
                                Category = "Sales",
                                Platform = "Website",
                                Reference = orderRef,
                            });

                            // Record cost of goods sold: each dress contributes an expense
                            // equal to its cost price, keyed by product so it stays idempotent
                            // (stock is one-of-one, so a dress sells exactly once).
                            foreach (Product soldProduct in soldProducts)
                            {
                                if (soldProduct.CostPrice <= 0) { continue; }
                                string cogsRef = $"cogs:{soldProduct.Id}";
                                if (await transactions.Query().AnyAsync(t => t.Reference == cogsRef)) { continue; }
                                ledgerEntries.Add(new Transaction
                                {
                                    Date = DateTime.UtcNow,
                                    Description = $"Cost of goods: {soldProduct.Name}",
                                    Amount = -soldProduct.CostPrice,
                                    Category = "Stock",
                                    Reference = cogsRef,
                                });
                            }
                        }

                        // Single save commits the Paid status, product/listing changes, and any
                        // new ledger entries together. AddRangeAsync also flushes the tracked
                        // mutations above (it runs SaveChanges even when the list is empty).
                        await transactions.AddRangeAsync(ledgerEntries);

                        // Notify the owner once, only on the first time we process this
                        // order's payment. Stripe retries the webhook, and the ledger
                        // Reference acts as the idempotency marker, so guarding on
                        // isNewSale ensures exactly one notification per sale.
                        if (isNewSale)
                        {
                            await emailService.SendOwnerSaleNotificationAsync(order);

                            // Email the customer their invoice. Never let a delivery failure
                            // fail the webhook — Stripe would retry and double-process the sale.
                            try
                            {
                                await emailService.SendOrderInvoiceEmailAsync(order);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to send customer invoice for order {OrderId}", order.Id);
                            }
                        }
                    }
                }
            }

            return true;
        }
        catch (StripeException)
        {
            return false;
        }
    }

    public async Task<List<OrderDto>> GetMyOrdersAsync(Guid userId)
    {
        List<Order> rows = await orders.Query()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        return rows.Select(ToDto).ToList();
    }

    public async Task<OrderDto?> GetByIdForViewerAsync(Guid id, Guid? userId, bool isAdmin)
    {
        Order? order = await orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return null;
        }

        // Verify ownership: authenticated users can only view their own orders (admins any).
        if (userId is Guid uid)
        {
            if (order.UserId != uid && !isAdmin)
            {
                return null;
            }
        }
        else if (order.UserId is not null)
        {
            // Unauthenticated user trying to access a registered user's order
            return null;
        }

        return ToDto(order);
    }

    public async Task<List<AdminOrderDto>> GetAllForAdminAsync()
    {
        List<Order> rows = await orders.Query()
            .Include(o => o.Items)
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        return rows.Select(ToAdminDto).ToList();
    }

    public async Task<AdminOrderDto?> UpdateStatusAsync(Guid id, UpdateOrderStatusDto dto)
    {
        Order? order = await orders.Query()
            .Include(o => o.Items)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return null;
        }

        string previousStatus = order.Status;
        order.Status = dto.Status;

        bool justDelivered =
            order.Status == "Delivered"
            && previousStatus != "Delivered"
            && order.ReviewRequestSentAtUtc is null
            && order.UserId is not null
            && order.User is not null
            && !string.IsNullOrWhiteSpace(order.User.Email);

        if (justDelivered)
        {
            order.ReviewRequestSentAtUtc = DateTime.UtcNow;
        }

        await orders.UpdateAsync(order);

        if (justDelivered)
        {
            try
            {
                await emailService.SendReviewRequestEmailAsync(
                    order.User!.Email,
                    order.User.FirstName ?? "",
                    order.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send review request email for order {OrderId}", order.Id);
            }
        }

        return ToAdminDto(order);
    }

    public async Task<SendInvoiceResult> SendInvoiceAsync(Guid id, string? platform)
    {
        Order? order = await orders.Query()
            .Include(o => o.Items)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return new SendInvoiceResult(SendInvoiceOutcome.NotFound, null);
        }

        string? recipient = order.User?.Email ?? order.GuestEmail;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return new SendInvoiceResult(SendInvoiceOutcome.NoEmail, null);
        }

        try
        {
            await emailService.SendOrderInvoiceEmailAsync(order, platform);
            return new SendInvoiceResult(SendInvoiceOutcome.Sent, recipient);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Manual invoice send failed for order {OrderId}", order.Id);
            return new SendInvoiceResult(SendInvoiceOutcome.Failed, null);
        }
    }

    public async Task<string?> BuildInvoicePreviewAsync(Guid id, string? platform)
    {
        Order? order = await orders.Query()
            .Include(o => o.Items)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return null;
        }

        return emailService.BuildOrderInvoiceHtml(order, platform);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        Order? order = await orders.GetByIdAsync(id);
        if (order is null)
        {
            return false;
        }

        await orders.DeleteAsync(id);
        return true;
    }

    /// <summary>
    /// The pieces in a just-paid order that something else had already sold.
    ///
    /// Stock is one-of-one and nothing reserves a piece during checkout — the availability
    /// check happens when the Stripe session is created, so two buyers can both pass it and
    /// both pay. This spots that after the fact.
    ///
    /// <paramref name="firstDelivery"/> is what keeps it honest: Stripe re-delivers webhook
    /// events, and on a retry the order is already Paid and its pieces already Sold — by this
    /// very order. Treating that as a conflict would alert on every single retry.
    /// </summary>
    public static List<Product> DetectDoubleSales(
        bool firstDelivery,
        IReadOnlyList<(Product Product, ProductStatus StatusBeforePayment)> beforePayment)
    {
        if (!firstDelivery)
        {
            return [];
        }
        return beforePayment
            .Where(p => p.StatusBeforePayment == ProductStatus.Sold)
            .Select(p => p.Product)
            .ToList();
    }

    /// <summary>
    /// Shouts about a piece that was paid for twice. There is no automatic remedy — one of the
    /// two buyers has to be refunded by hand — so the only failure worth avoiding is nobody
    /// noticing. Logs at Critical and emails the operator; an email failure must not break the
    /// webhook (Stripe would retry it and re-run everything).
    /// </summary>
    private async Task AlertDoubleSaleAsync(Order order, List<Product> alreadySold)
    {
        string names = string.Join(", ", alreadySold.Select(p => $"{p.Name} ({p.Sku})"));
        logger.LogCritical(
            "Double sale: order {OrderId} was paid for {Count} piece(s) already marked Sold — {Names}. Manual refund required.",
            order.Id, alreadySold.Count, names);

        try
        {
            string? recipient = configuration["Email:SaleNotificationRecipient"];
            if (string.IsNullOrWhiteSpace(recipient))
            {
                return;
            }
            string body =
                $"Order {order.Id} has been paid, but {alreadySold.Count} item(s) in it were already sold:\n\n"
                + names
                + "\n\nStock is one-of-one, so this piece has been paid for twice and one buyer "
                + $"needs refunding. Customer on this order: {order.User?.Email ?? order.GuestEmail ?? "unknown"}."
                + "\n\nRefund via the Stripe dashboard, then reconcile the Sales entries on the admin Finance tab.";
            await emailService.SendOperatorReminderEmailAsync(recipient, "Double sale needs a refund", body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send the double-sale alert for order {OrderId}", order.Id);
        }
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id,
        o.Status,
        o.Total,
        o.CreatedAtUtc,
        o.Items.Select(i => new OrderItemDto(
            i.ProductId, i.ProductName, i.UnitPrice, i.Quantity
        )).ToList()
    );

    private static AdminOrderDto ToAdminDto(Order o) => new(
        o.Id,
        o.Status,
        o.Total,
        o.CreatedAtUtc,
        o.User?.Email ?? o.GuestEmail ?? "Unknown",
        o.User is not null ? $"{o.User.FirstName} {o.User.LastName}" : null,
        o.Items.Select(i => new OrderItemDto(
            i.ProductId, i.ProductName, i.UnitPrice, i.Quantity
        )).ToList()
    );
}
