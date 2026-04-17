using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Support;

public static class RetailOrderStockCommitter
{
    public static async Task EnsureStockDeductedAsync(
        IGarageContext db,
        long retailOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await db.RetailOrders
            .Include(x => x.RetailOrderItems)
            .FirstOrDefaultAsync(x => x.Id == retailOrderId, cancellationToken);

        if (order is null)
        {
            return;
        }

        var requiredByProduct = order.RetailOrderItems
            .Where(x => x.Quantity > 0)
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        if (requiredByProduct.Count == 0)
        {
            return;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var existingOutByProduct = await db.StockTransactions
                    .Where(x =>
                        x.RetailOrderId == retailOrderId &&
                        x.Type == "OUT")
                    .GroupBy(x => x.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        Quantity = g.Sum(x => x.Quantity)
                    })
                    .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);

                var remainingByProduct = requiredByProduct
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value - (existingOutByProduct.TryGetValue(kvp.Key, out var alreadyOut) ? alreadyOut : 0m));

                var targetProductIds = remainingByProduct
                    .Where(x => x.Value > 0)
                    .Select(x => x.Key)
                    .ToList();

                if (targetProductIds.Count == 0)
                {
                    await tx.CommitAsync(cancellationToken);
                    return;
                }

                var inventoryRows = await db.Inventories
                    .Include(x => x.Warehouse)
                    .Where(x =>
                        targetProductIds.Contains(x.ProductId) &&
                        x.Warehouse.BranchId == order.BranchId &&
                        x.Warehouse.IsDeleted != true &&
                        x.Warehouse.IsActive != false)
                    .ToListAsync(cancellationToken);

                var now = DateTime.UtcNow;
                var orderCode = string.IsNullOrWhiteSpace(order.OrderCode) ? $"ID:{order.Id}" : order.OrderCode.Trim();

                foreach (var productId in targetProductIds)
                {
                    var need = remainingByProduct[productId];
                    var candidates = SelectWarehouseCandidates(inventoryRows.Where(x => x.ProductId == productId));

                    foreach (var inventory in candidates)
                    {
                        if (need <= 0) break;

                        var reservedQuantity = inventory.ReservedQuantity ?? 0m;
                        var availableQuantity = inventory.Quantity - reservedQuantity;
                        if (availableQuantity <= 0) continue;

                        var consume = Math.Min(need, availableQuantity);
                        inventory.Quantity -= consume;
                        need -= consume;

                        db.StockTransactions.Add(new StockTransaction
                        {
                            ProductId = productId,
                            WarehouseId = inventory.WarehouseId,
                            Type = "OUT",
                            Quantity = consume,
                            Note = $"Auto deduct stock for retail order {orderCode}",
                            RetailOrderId = retailOrderId,
                            CreatedAt = now
                        });
                    }

                    if (need > 0)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock to fulfill retail order {orderCode}. ProductId={productId}, Remaining={need}");
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private static List<Inventory> SelectWarehouseCandidates(IEnumerable<Inventory> rows)
    {
        var all = rows
            .Where(x => (x.Quantity - (x.ReservedQuantity ?? 0m)) > 0)
            .ToList();

        if (all.Count == 0)
        {
            return all;
        }

        var preferred = all
            .Where(x => IsPreferredRetailWarehouse(x.Warehouse.Type))
            .OrderBy(x => x.WarehouseId)
            .ToList();

        if (preferred.Count > 0)
        {
            return preferred;
        }

        return all
            .Where(x => !IsExcludedWarehouse(x.Warehouse.Type))
            .OrderBy(x => x.WarehouseId)
            .ToList();
    }

    private static bool IsPreferredRetailWarehouse(string? warehouseType)
    {
        var normalized = NormalizeWarehouseType(warehouseType);
        return normalized.Contains("SELLABLE", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("RETAIL", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcludedWarehouse(string? warehouseType)
    {
        var normalized = NormalizeWarehouseType(warehouseType);
        return normalized.Contains("DEFECTIVE", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("TRANSIT", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeWarehouseType(string? warehouseType)
        => string.IsNullOrWhiteSpace(warehouseType) ? string.Empty : warehouseType.Trim().ToUpperInvariant();
}
