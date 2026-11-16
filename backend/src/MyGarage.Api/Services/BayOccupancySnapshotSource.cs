using System.Text.Json;
using MyGarage.Application.Bays;
using MyGarage.Controllers;

namespace MyGarage.Services;

public sealed class BayOccupancySnapshotSource(IWebHostEnvironment environment) : IBayOccupancySnapshotSource
{
    public async Task<BayOccupancySnapshot> ReadAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(environment.ContentRootPath, "Configurations", "bay-admin-settings.json");
        if (!File.Exists(path)) return new BayOccupancySnapshot([], new Dictionary<long, int>());
        try
        {
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<BayAdminController.BayAdminSettings>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct)
                ?? throw new InvalidOperationException("Không đọc được dữ liệu bãi đỗ.");
            settings.Normalize();
            return new BayOccupancySnapshot(settings.Sessions.Select(session => new BayOccupancySession(session.BayId, session.VehicleId, session.AssignedAt, session.ReleasedAt, session.Note)).ToList(),
                settings.BayMetadata.ToDictionary(pair => pair.Key, pair => pair.Value.Capacity));
        }
        catch (Exception ex) when (ex is IOException or JsonException) { throw new InvalidOperationException("Chưa đọc được dữ liệu vị trí bãi đỗ. Vui lòng tải lại.", ex); }
    }
}
