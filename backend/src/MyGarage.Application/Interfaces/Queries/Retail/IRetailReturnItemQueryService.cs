using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record RetailReturnParentInfo(string? Status, long RetailOrderId, long BranchId);

public interface IRetailReturnItemQueryService
{
    Task<(IReadOnlyList<RetailReturnItem> Items, int Total)> GetPageAsync(long? branchId, int page, int pageSize, CancellationToken ct = default);
    Task<RetailReturnItem?> GetByIdScopedAsync(long id, long? branchId, CancellationToken ct = default);
    Task<RetailReturnParentInfo?> GetParentForCreateAsync(long retailReturnId, long? branchId, CancellationToken ct = default);
    Task<bool> ValidateOrderItemAndWarehouseAsync(long retailOrderItemId, long retailOrderId, long warehouseId, long branchId, CancellationToken ct = default);
    Task<RetailReturnItem?> GetForUpdateScopedAsync(long id, long? branchId, CancellationToken ct = default);
    Task<bool> WarehouseBelongsToBranchAsync(long warehouseId, long branchId, CancellationToken ct = default);
    Task<string?> GetParentStatusScopedAsync(long id, long? branchId, CancellationToken ct = default);
}
