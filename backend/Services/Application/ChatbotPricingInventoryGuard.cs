using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotPricingInventoryGuard : IChatbotPricingInventoryGuard
{
    private readonly IGarageContext _context;
    private readonly ILogger<ChatbotPricingInventoryGuard> _logger;

    public ChatbotPricingInventoryGuard(
        IGarageContext context,
        ILogger<ChatbotPricingInventoryGuard> logger)
    {
        _context = context;
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

        var product = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsDeleted != true && (p.IsActive == null || p.IsActive == true))
            .OrderByDescending(p => p.Sku.ToLower() == loweredHint)
            .ThenByDescending(p => p.Name.ToLower() == loweredHint)
            .ThenByDescending(p => p.Sku.ToLower().Contains(loweredHint))
            .ThenByDescending(p => p.Name.ToLower().Contains(loweredHint))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.RetailPrice,
                p.ServicePrice
            })
            .FirstOrDefaultAsync(cancellationToken);

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

        var inventories = _context.Inventories
            .AsNoTracking()
            .Where(i => i.ProductId == product.Id);

        if (branchId.HasValue)
        {
            inventories = inventories.Where(i => i.Warehouse.BranchId == branchId.Value);
        }

        var stockProjection = await inventories
            .Select(i => new
            {
                i.Quantity,
                Reserved = i.ReservedQuantity ?? 0m
            })
            .ToListAsync(cancellationToken);

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
