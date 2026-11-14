using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;

namespace MyGarage.Application.GatePasses;

public interface IGatePassAndApprovalApplicationService
{
    Task<GatePassItemDto> IssueGatePassAsync(
        IssueGatePassCommand command,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<GatePassItemDto?> ScanGatePassAsync(
        long gatePassId,
        ScanGatePassCommand command,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<ApprovalRequestItemDto> CreateApprovalRequestAsync(
        CreateApprovalRequestCommand command,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<ApprovalRequestItemDto?> DecideApprovalRequestAsync(
        long approvalRequestId,
        DecideApprovalRequestCommand command,
        UserActorContext actor,
        CancellationToken ct = default);
}
