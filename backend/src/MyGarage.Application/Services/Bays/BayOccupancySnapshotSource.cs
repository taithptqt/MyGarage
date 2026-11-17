using MyGarage.Application.Interfaces.Stores.Common;

namespace MyGarage.Application.Bays;

public sealed class BayOccupancySnapshotSource(IAppConfigFileStore configStore) : IBayOccupancySnapshotSource
{
    public async Task<BayOccupancySnapshot> ReadAsync(CancellationToken ct = default)
    {
        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(configStore, ct);
        return new BayOccupancySnapshot(
            settings.Sessions.Select(session => new BayOccupancySession(session.BayId, session.VehicleId, session.AssignedAt, session.ReleasedAt, session.Note)).ToList(),
            settings.BayMetadata.ToDictionary(pair => pair.Key, pair => pair.Value.Capacity));
    }
}
