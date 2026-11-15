using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.GatePasses;

public interface IGatePassAndApprovalStore
{
    Task<(bool Exists, long? ExecuteBranchId, string? Status)> GetServiceOrderInfoAsync(
        long serviceOrderId,
        CancellationToken ct = default);

    Task AddGatePassAsync(GatePass entity, CancellationToken ct = default);

    Task<GatePass?> GetGatePassByIdAsync(long gatePassId, CancellationToken ct = default);

    Task SaveGatePassAsync(GatePass entity, CancellationToken ct = default);

    Task AddApprovalRequestAsync(ApprovalRequest entity, CancellationToken ct = default);

    Task<ApprovalRequest?> GetApprovalRequestByIdAsync(long approvalRequestId, CancellationToken ct = default);

    Task SaveApprovalDecisionAsync(
        ApprovalRequest entity,
        bool approveLinkedGatePass,
        long approverUserId,
        CancellationToken ct = default);
}
