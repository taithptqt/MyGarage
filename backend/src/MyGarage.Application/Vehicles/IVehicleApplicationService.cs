using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Vehicles.DTOs;

namespace MyGarage.Application.Vehicles;

public interface IVehicleApplicationService
{
    Task<VehicleAdminPageResult> GetVehicleAdminPageAsync(VehicleAdminPageQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<VehicleDetailDto?> GetVehicleDetailAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<VehicleDetailDto> CreateVehicleAsync(VehicleUpsertCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<VehicleDetailDto?> UpdateVehicleAsync(long id, VehicleUpsertCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<bool> ChangeOwnerAsync(VehicleOwnerChangeCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<bool> HideVehicleAsync(long vehicleId, UserActorContext actor, CancellationToken ct = default);
}
