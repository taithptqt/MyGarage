using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotPricingInventoryGuard : IChatbotPricingInventoryGuard
{
    private readonly IChatbotStore _store;
    private readonly IAppLogger<ChatbotPricingInventoryGuard> _logger;

    public ChatbotPricingInventoryGuard(
        IChatbotStore store,
        IAppLogger<ChatbotPricingInventoryGuard> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<ChatbotPricingInventoryResult> ResolveAsync(
        long customerId,
        long? branchId,
        string productHint,
        CancellationToken cancellationToken = default)
    {
        var normalizedHint = (productHint ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedHint))
        {
            return new ChatbotPricingInventoryResult
            {
                ProductHint = string.Empty,
                ProductFound = false
            };
        }

        var loweredHint = normalizedHint.ToLowerInvariant();

        var product = await _store.FindBestActiveProductAsync(loweredHint, cancellationToken);

        if (product is null)
        {
            _logger.LogInformation(
                "Chatbot pricing guard: product unavailable. CustomerId={CustomerId}, BranchId={BranchId}, Hint={Hint}",
                customerId,
                branchId,
                normalizedHint);
            return new ChatbotPricingInventoryResult
            {
                ProductHint = normalizedHint,
                ProductFound = false
            };
        }

        var price = product.RetailPrice ?? product.ServicePrice;
        var hasPrice = price.HasValue;
        var priceSource = product.RetailPrice.HasValue
            ? "Products.RetailPrice"
            : product.ServicePrice.HasValue
                ? "Products.ServicePrice"
                : "UNAVAILABLE";

        var stockProjection = await _store.GetProductInventoryStockAsync(product.Id, branchId, cancellationToken);

        var hasStockRecord = stockProjection.Count > 0;
        var stockQuantity = hasStockRecord
            ? stockProjection.Sum(x => x.Quantity - x.Reserved)
            : (decimal?)null;

        return new ChatbotPricingInventoryResult
        {
            ProductHint = normalizedHint,
            ProductFound = true,
            ProductId = product.Id,
            ProductName = product.Name,
            ProductSku = product.Sku,
            PriceAvailable = hasPrice,
            Price = hasPrice ? price : null,
            PriceSource = priceSource,
            StockAvailable = hasStockRecord,
            StockQuantity = hasStockRecord ? stockQuantity : null,
            StockSource = hasStockRecord ? "Inventories(+Warehouses)" : "UNAVAILABLE"
        };
    }
}
