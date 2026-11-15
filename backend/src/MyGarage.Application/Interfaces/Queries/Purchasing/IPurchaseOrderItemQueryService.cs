using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IPurchaseOrderItemQueryService
{
    Task<IReadOnlyList<PurchaseOrderItem>> GetAllByBranchAsync(long? branchId, CancellationToken ct = default);
    Task<(IReadOnlyList<PurchaseOrderItem> Items, int Total)> GetPageByBranchAsync(
        long? branchId, int page, int pageSize, CancellationToken ct = default);
}
