using Microsoft.AspNetCore.SignalR;
using MyGarage.Application.Interfaces.Notifications;
using MyGarage.Hubs;

namespace MyGarage.Services.Realtime;

public sealed class SignalRDiagnosisRealtimeNotifier : IDiagnosisRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;

    public SignalRDiagnosisRealtimeNotifier(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifySupplementaryProposalUpdatedAsync(
        IReadOnlyCollection<long> recipientStaffIds,
        SupplementaryProposalRealtimeEvent evt,
        CancellationToken ct = default)
    {
        if (recipientStaffIds.Count == 0)
        {
            return;
        }

        var payload = new
        {
            eventType = "SUPPLEMENTARY_PROPOSAL",
            action = (evt.Action ?? "UPDATED").Trim().ToUpperInvariant(),
            serviceOrderId = evt.ServiceOrderId,
            serviceOrderCode = evt.ServiceOrderCode,
            diagnosisId = evt.DiagnosisId,
            recommendationId = evt.RecommendationId,
            executeBranchId = evt.ExecuteBranchId,
            originBranchId = evt.OriginBranchId,
            occurredAt = evt.OccurredAt
        };

        foreach (var staffId in recipientStaffIds)
        {
            await _hubContext.Clients.Group($"User_{staffId}")
                .SendAsync("SupplementaryProposalUpdated", payload, ct);
        }
    }
}
