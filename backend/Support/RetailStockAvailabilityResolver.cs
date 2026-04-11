using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Support;

public sealed record RetailStockAvailabilityEntry(
    long ProductId,
    long? BranchId,
    int AvailableQuantity
);

public sealed record RetailBranchAvailabilityRow(
    long BranchId,
    string BranchName,
    int AvailableQuantity
);

public static class RetailStockAvailabilityResolver
{
    public static async Task<IReadOnlyList<RetailBranchAvailabilityRow>> GetRetailStockBreakdownByProductAsync(
        IGarageContext db,
        long productId,
        CancellationToken cancellationToken = default)
    {
        var branchRows = await db.Branches
            .AsNoTracking()
            .Where(branch => branch.IsDeleted != true)
            .OrderBy(branch => branch.Name)
            .ThenBy(branch => branch.Id)
            .Select(branch => new { branch.Id, branch.Name })
            .ToListAsync(cancellationToken);

        var inventoryRows = await db.Inventories
            .AsNoTracking()
            .Include(inventory => inventory.Warehouse)
            .Where(inventory =>
                inventory.ProductId == productId &&
                inventory.Warehouse.IsDeleted != true &&
                inventory.Warehouse.IsActive != false)
            .Select(inventory => new InventoryStockRow(
                inventory.ProductId,
                inventory.Warehouse.BranchId,
                inventory.Warehouse.Type,
                inventory.Quantity - (inventory.ReservedQuantity ?? 0m)))
            .ToListAsync(cancellationToken);

        var rowsByBranch = inventoryRows
            .GroupBy(row => row.BranchId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var result = new List<RetailBranchAvailabilityRow>(branchRows.Count);

        foreach (var branch in branchRows)
        {
            if (!rowsByBranch.TryGetValue(branch.Id, out var warehouseRows))
            {
                result.Add(new RetailBranchAvailabilityRow(branch.Id, branch.Name ?? string.Empty, 0));
                continue;
            }

            var quantity = ComputeAvailabilityForWarehouseRows(warehouseRows);
            result.Add(new RetailBranchAvailabilityRow(branch.Id, branch.Name ?? string.Empty, quantity));
        }

        return result;
    }

    public static async Task<IReadOnlyDictionary<long, int>> GetAvailableQuantitiesAsync(
        IGarageContext db,
        IEnumerable<long> productIds,
        long? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedProductIds = productIds
            .Where(productId => productId > 0)
            .Distinct()
            .ToList();

        if (normalizedProductIds.Count == 0)
        {
            return new Dictionary<long, int>();
        }

        var rows = await db.Inventories
            .AsNoTracking()
            .Include(inventory => inventory.Warehouse)
            .Where(inventory =>
                normalizedProductIds.Contains(inventory.ProductId) &&
                inventory.Warehouse.IsDeleted != true &&
                inventory.Warehouse.IsActive != false &&
                (!branchId.HasValue || inventory.Warehouse.BranchId == branchId.Value))
            .Select(inventory => new InventoryStockRow(
                inventory.ProductId,
                inventory.Warehouse.BranchId,
                inventory.Warehouse.Type,
                inventory.Quantity - (inventory.ReservedQuantity ?? 0m)))
            .ToListAsync(cancellationToken);

        var availability = normalizedProductIds.ToDictionary(productId => productId, _ => 0);

        foreach (var group in rows.GroupBy(row => row.ProductId))
        {
            availability[group.Key] = ComputeAvailabilityForWarehouseRows(group.ToList());
        }

        return availability;
    }

    private static int ComputeAvailabilityForWarehouseRows(IReadOnlyList<InventoryStockRow> warehouseRows)
    {
        if (warehouseRows.Count == 0)
        {
            return 0;
        }

        var preferredRows = warehouseRows.Where(row => IsPreferredRetailWarehouse(row.WarehouseType)).ToList();
        var candidateRows = preferredRows.Count > 0
            ? preferredRows
            : warehouseRows.Where(row => !IsExcludedWarehouse(row.WarehouseType)).ToList();

        if (candidateRows.Count == 0)
        {
            return 0;
        }

        return ToWholeUnits(candidateRows.Sum(row => row.AvailableQuantity));
    }

    public static async Task<IReadOnlyList<RetailStockAvailabilityEntry>> GetAvailabilityEntriesAsync(
        IGarageContext db,
        IEnumerable<long> productIds,
        long? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var availability = await GetAvailableQuantitiesAsync(db, productIds, branchId, cancellationToken);

        return availability
            .OrderBy(entry => entry.Key)
            .Select(entry => new RetailStockAvailabilityEntry(entry.Key, branchId, entry.Value))
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

    private static int ToWholeUnits(decimal availableQuantity)
    {
        var normalized = Math.Max(0m, decimal.Floor(availableQuantity));
        return normalized > int.MaxValue ? int.MaxValue : (int)normalized;
    }

    private sealed record InventoryStockRow(
        long ProductId,
        long BranchId,
        string? WarehouseType,
        decimal AvailableQuantity
    );
}
