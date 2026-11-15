using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IVehicleQueryService
{
    Task<(IReadOnlyList<Vehicle> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<bool> CustomerOwnsVehicleAsync(long vehicleId, long customerId, CancellationToken ct = default);
}
