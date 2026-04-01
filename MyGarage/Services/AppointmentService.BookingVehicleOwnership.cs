using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task ValidateSelectedVehicleOwnershipAsync(long customerId, long? vehicleId)
        {
            if (!vehicleId.HasValue) return;

            var existingVehicle = await _context.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == vehicleId.Value);
            if (existingVehicle == null)
                throw new InvalidOperationException("Kh\u00f4ng t\u00ecm th\u1ea5y xe \u0111\u00e3 ch\u1ecdn.");

            var customerOwnsVehicle = await _context.CustomerVehicles
                .AsNoTracking()
                .AnyAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId.Value && cv.IsDeleted != true);
            if (!customerOwnsVehicle)
                throw new InvalidOperationException("Xe \u0111\u00e3 ch\u1ecdn kh\u00f4ng thu\u1ed9c t\u00e0i kho\u1ea3n kh\u00e1ch h\u00e0ng.");
        }

        private async Task<long> ResolveOrCreateVehicleAsync(BookAppointmentRequestDto dto, long? actorStaffId)
        {
            var plate = dto.LicensePlate!.Trim().ToUpper();
            var existing = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == plate);
            if (existing != null)
            {
                if (existing.Vin == null && !string.IsNullOrWhiteSpace(dto.Vin))
                {
                    existing.Vin = dto.Vin.Trim();
                    await _context.SaveChangesAsync();
                }

                return existing.Id;
            }

            var newVehicle = new Vehicle
            {
                LicensePlate = plate,
                Vin = dto.Vin,
                ModelId = dto.VehicleModelId!.Value,
                Color = dto.Color,
                CreatedBy = actorStaffId,
                CreatedAt = _clock.UtcNow
            };
            _context.Vehicles.Add(newVehicle);
            await _context.SaveChangesAsync();
            return newVehicle.Id;
        }

        private async Task EnsureCustomerVehicleLinkAsync(long customerId, long vehicleId, long? actorStaffId)
        {
            var link = await _context.CustomerVehicles.FirstOrDefaultAsync(
                cv => cv.CustomerId == customerId &&
                      cv.VehicleId == vehicleId &&
                      cv.IsDeleted != true);

            if (link == null)
            {
                _context.CustomerVehicles.Add(new CustomerVehicle
                {
                    CustomerId = customerId,
                    VehicleId = vehicleId,
                    IsCurrentOwner = true,
                    OwnershipStartDate = _clock.LocalToday,
                    CreatedBy = actorStaffId,
                    CreatedAt = _clock.UtcNow
                });
                await _context.SaveChangesAsync();
                return;
            }

            if (link.CreatedBy == null)
            {
                link.CreatedBy = actorStaffId;
                await _context.SaveChangesAsync();
            }
        }
    }
}