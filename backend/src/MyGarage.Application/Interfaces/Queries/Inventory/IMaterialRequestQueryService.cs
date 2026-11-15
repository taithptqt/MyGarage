using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IMaterialRequestQueryService
{
    Task<(IReadOnlyList<MaterialRequest> Items, int Total)> GetPageAsync(
        long? scopedBranchId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<MaterialRequest?> GetByIdDetailedAsync(long id, CancellationToken ct = default);
    Task<MaterialRequest?> GetWithDetailsByIdAsync(long id, CancellationToken ct = default);
    Task<bool> ExistsAsync(long id, CancellationToken ct = default);
    Task<long?> GetServiceOrderExecuteBranchIdAsync(long serviceOrderId, CancellationToken ct = default);
    Task<bool> BelongsToBranchScopeAsync(long materialRequestId, long? scopedBranchId, CancellationToken ct = default);
    Task<Dictionary<(long ProductId, long WarehouseId), MyGarage.Models.Inventory>> LoadInventoryLookupAsync(
        IEnumerable<MaterialRequest> requests,
        CancellationToken ct = default);
}
