using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class ServiceOrderService
    {
        private async Task EnsureWorkshopSelectionCompatibleWithVehicleAsync(
            WorkshopServiceSelection selection,
            long vehicleId)
        {
            if (selection == null || vehicleId <= 0)
            {
                return;
            }

            var vehicleModelProjection = await _context.Vehicles
                .AsNoTracking()
                .Where(vehicle => vehicle.Id == vehicleId && vehicle.IsDeleted != true)
                .Select(vehicle => new
                {
                    vehicle.ModelId,
                    FuelType = vehicle.Model.FuelType
                })
                .FirstOrDefaultAsync();

            if (vehicleModelProjection == null || vehicleModelProjection.ModelId <= 0)
            {
                return;
            }

            if (EngineTypeCompatibilityRules.ParseVehicleEngineFamilies(vehicleModelProjection.FuelType).Count == 0)
            {
                return;
            }

            var incompatibleServices = (selection.ServiceMap?.Values ?? new Dictionary<long, StandardService>().Values)
                .Where(service => !EngineTypeCompatibilityRules.IsServiceCompatibleWithVehicleFuel(service.EngineType, vehicleModelProjection.FuelType))
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
