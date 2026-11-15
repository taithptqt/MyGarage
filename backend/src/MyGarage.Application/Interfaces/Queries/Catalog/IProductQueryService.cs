using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

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

public sealed record ProductRetailCatalogResult(
    IReadOnlyList<Product> PageRows,
    int Total,
    int ActiveCount,
    IReadOnlyList<ProductImage> Images,
    IReadOnlyDictionary<long, int> Availability);

public sealed record LowStockProductRow(
    long productId,
    string sku,
    string productName,
    long warehouseId,
    string warehouseName,
    long branchId,
    string branchName,
    decimal quantity,
    decimal minStock,
    decimal reorderPoint,
    string status,
    decimal deficit);

public interface IProductQueryService
{
    Task<(IReadOnlyList<Product> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<bool> BranchExistsAsync(long branchId, CancellationToken ct = default);
    Task<ProductRetailCatalogResult> GetRetailCatalogAsync(
        string? search,
        long? categoryId,
        long? branchId,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<IReadOnlyList<RetailStockAvailabilityEntry>> GetStockAvailabilityAsync(
        IReadOnlyList<long> productIds,
        long? branchId,
        CancellationToken ct = default);
    Task<IReadOnlyList<LowStockProductRow>> GetLowStockProductsAsync(
        long? branchId,
        int take,
        CancellationToken ct = default);
    Task<bool> ActiveProductExistsAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<RetailBranchAvailabilityRow>> GetRetailStockBreakdownByProductAsync(
        long productId,
        CancellationToken ct = default);
}
