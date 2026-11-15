namespace MyGarage.Application.Bays;

public interface IBayRealtimeNotifier
{
    Task BroadcastBayTimelineUpdatedAsync(long branchId, CancellationToken ct = default);
}
