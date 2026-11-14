using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface ISharedOperationsAuthorizationService
{
    Task<bool> CanManageGatePassesAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessApprovalRequestsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanDecideApprovalRequestAsync(long approvalRequestId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessAuditLogsAsync(UserActorContext actor, CancellationToken ct = default);
}
