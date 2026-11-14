using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IBayAuthorizationService
{
    Task<bool> CanAccessBayAsync(long bayId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanAccessBranchBaysAsync(long branchId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanAllocateBayAsync(long bayId, long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanReleaseBayAsync(long bayId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanTransferBayAsync(long fromBayId, long toBayId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanAdministerBaysAsync(long branchId, UserActorContext actor, CancellationToken cancellationToken = default);
}
