namespace MyGarage.Application.Interfaces.Queries;

public interface IBayVehicleLookupDataService
{
    Task<Dictionary<long, BayVehicleDescriptor>> LoadVehicleMapAsync(IEnumerable<long> vehicleIds, CancellationToken ct = default);

    Task<BayVehicleDescriptor?> LoadVehicleDescriptorAsync(long vehicleId, CancellationToken ct = default);

    Task<long?> FindOccupiedBayIdByVehicleAsync(long vehicleId, long? ignoreBayId = null, CancellationToken ct = default);
}
