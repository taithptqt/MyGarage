using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IInventoryAuthorizationService
{
    Task<bool> CanAccessWarehouseAsync(long warehouseId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanAccessBranchInventoryAsync(long branchId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanMutateInventoryAsync(long warehouseId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanViewStockTransactionsAsync(long? branchId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanManageStockAlertsAsync(long? branchId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanIssueMaterialRequestAsync(long materialRequestId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanProcessMaterialReturnAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default);
}
