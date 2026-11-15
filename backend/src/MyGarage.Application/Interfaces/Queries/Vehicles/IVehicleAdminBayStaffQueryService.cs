using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IVehicleAdminBayStaffQueryService
{
    Task<Staff?> GetActiveStaffByIdAsync(long staffId, CancellationToken ct = default);
    Task<string?> GetStaffFullNameAsync(long staffId, CancellationToken ct = default);

    Task<Bay?> GetActiveBayByIdAsync(long bayId, CancellationToken ct = default);
    Task<Bay?> GetBayByIdAsync(long bayId, CancellationToken ct = default);
    Task<IReadOnlyList<Bay>> GetActiveBaysOccupiedByVehicleAsync(long vehicleId, CancellationToken ct = default);
    Task<Bay?> GetFirstActiveBayOccupiedByVehicleAsync(long vehicleId, CancellationToken ct = default);
    Task<long?> FindOccupiedBayIdByVehicleAsync(long vehicleId, CancellationToken ct = default);
}
