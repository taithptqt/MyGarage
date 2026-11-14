using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IServiceOrderAuthorizationService
{
    /// <summary>
    /// Verifies if actor has read access to the service order.
    /// Staff must match ExecuteBranchId (or OriginBranchId). Customers must match CustomerId.
    /// </summary>
    Task<bool> CanAccessServiceOrderAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies if actor can modify workshop fields, status, or details.
    /// Requires Staff matching ExecuteBranchId.
    /// </summary>
    Task<bool> CanMutateWorkshopAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies if actor can dispatch order to a bay and assign technicians.
    /// Requires Dispatcher, Foreman, or Manager at ExecuteBranchId.
    /// </summary>
    Task<bool> CanDispatchOrderAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies if actor can log work (start, pause, complete) on a specific service order detail.
    /// Requires assigned Technician or Foreman at ExecuteBranchId.
    /// </summary>
    Task<bool> CanLogTechnicianWorkAsync(long serviceOrderId, long serviceOrderDetailId, UserActorContext actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies if actor can perform QC inspection and submit PASS/FAIL decision.
    /// Requires QC Inspector or Foreman at ExecuteBranchId.
    /// Invariant: Must not be the primary technician who worked on the order!
    /// </summary>
    Task<bool> CanPerformQcInspectionAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies if actor can execute handover to customer.
    /// Requires Service Advisor, Front Desk, or Manager at ExecuteBranchId.
    /// </summary>
    Task<bool> CanPerformHandoverAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies if actor can approve or reject an edit request.
    /// Requires Foreman, Manager, or Admin at the corresponding branch.
    /// </summary>
    Task<bool> CanDecideEditRequestAsync(long requestId, UserActorContext actor, CancellationToken cancellationToken = default);
}
