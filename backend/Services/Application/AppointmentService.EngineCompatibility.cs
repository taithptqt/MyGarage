using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task EnsureRequestedServicesCompatibleWithVehicleEngineAsync(
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds)
        {
            var resolvedVehicleModelId = vehicleModelId.GetValueOrDefault();
            if (resolvedVehicleModelId <= 0)
            {
                return;
            }

            var vehicleFuelType = await _context.VehicleModels
                .AsNoTracking()
                .Where(model => model.Id == resolvedVehicleModelId && model.IsDeleted != true)
                .Select(model => model.FuelType)
                .FirstOrDefaultAsync();

            if (EngineTypeCompatibilityRules.ParseVehicleEngineFamilies(vehicleFuelType).Count == 0)
            {
                return;
            }

            var requestedServices = await LoadRequestedServicesWithPackageItemsAsync(requestedServiceIds, requestedPackageIds);
            var incompatibleServices = requestedServices
                .Where(service => !EngineTypeCompatibilityRules.IsServiceCompatibleWithVehicleFuel(service.EngineType, vehicleFuelType))
                .Select(service => service.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (incompatibleServices.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"\u0044\u1ECBch v\u1EE5 kh\u00F4ng ph\u00F9 h\u1EE3p v\u1EDBi lo\u1EA1i \u0111\u1ED9ng c\u01A1 c\u1EE7a d\u00F2ng xe \u0111\u00E3 ch\u1ECDn: {string.Join(", ", incompatibleServices)}.");
        }
    }
}
