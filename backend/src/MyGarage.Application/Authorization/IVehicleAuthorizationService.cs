using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IVehicleAuthorizationService
{
    Task<bool> CanAccessVehicleAsync(long vehicleId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanModifyVehicleAsync(long vehicleId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessCustomerVehicleAsync(long customerVehicleId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAdministerVehiclesAsync(UserActorContext actor, CancellationToken ct = default);
}
