using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.CustomerPortal.DTOs;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Options;

namespace MyGarage.Application.CustomerPortal;

public interface ICustomerPortalVehicleStore
{
    Task<IReadOnlyList<CustomerPortalVehicleItemDto>> GetCustomerVehiclesAsync(long customerId, CancellationToken ct = default);

    Task<CustomerPortalModelInfo?> GetActiveModelInfoAsync(long modelId, CancellationToken ct = default);

    Task<Vehicle?> GetActiveVehicleByPlateAsync(string normalizedPlate, CancellationToken ct = default);

    Task<Vehicle?> GetActiveVehicleByIdAsync(long vehicleId, CancellationToken ct = default);

    Task<bool> HasOtherActiveOwnerAsync(long vehicleId, long customerId, CancellationToken ct = default);

    Task<bool> VinExistsOnOtherVehicleAsync(string normalizedVin, long? excludeVehicleId, CancellationToken ct = default);

    Task<bool> PlateExistsOnOtherVehicleAsync(string normalizedPlate, long excludeVehicleId, CancellationToken ct = default);

    Task<Vehicle> UpsertCustomerVehicleAsync(
        long customerId,
        Vehicle? existingVehicle,
        Vehicle? newVehicle,
        long? staffId,
        DateTime now,
        CancellationToken ct = default);

    Task SaveVehicleUpdateAsync(Vehicle vehicle, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerPortalVehicleDto>> GetPortalVehiclesAsync(long customerId, CancellationToken ct = default);

    Task<CustomerPortalVehicleDto?> GetSinglePortalVehicleDtoAsync(long customerId, long vehicleId, CancellationToken ct = default);

    Task<CustomerPortalVehicleHistoryDto> GetVehicleHistoryAsync(
        long customerId,
        long vehicleId,
        ProactiveMaintenanceOutreachOptions maintenanceSettings,
        CancellationToken ct = default);

    Task<CustomerPortalVehicleLookupDto> GetVehicleLookupsAsync(CancellationToken ct = default);
}
