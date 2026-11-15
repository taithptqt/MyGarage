using Microsoft.AspNetCore.SignalR;
using MyGarage.Application.Bays;
using MyGarage.Hubs;

namespace MyGarage.Services.Realtime;

public sealed class SignalRBayRealtimeNotifier : IBayRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;

    public SignalRBayRealtimeNotifier(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastBayTimelineUpdatedAsync(long branchId, CancellationToken ct = default)
    {
        if (branchId <= 0)
        {
            return;
        }

        await _hubContext.Clients
            .Group($"WorkshopBranch_{branchId}")
            .SendAsync("BayTimelineUpdated", new
            {
                branchId,
                changedAt = VietnamTime.Now
            }, ct);
    }
}
