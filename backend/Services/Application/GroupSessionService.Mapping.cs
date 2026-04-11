using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class GroupSessionService
    {
        private static GroupSessionResponseDto MapHeaderOnly(GroupSession source)
        {
            return new GroupSessionResponseDto
            {
                Id = source.Id,
                GroupCode = source.GroupCode,
                CustomerId = source.CustomerId,
                BranchId = source.BranchId,
                Status = source.Status,
                TotalAppointments = source.TotalAppointments,
                ConfirmedAppointments = source.ConfirmedAppointments,
                CanceledAppointments = source.CanceledAppointments,
                ConfirmedAt = source.ConfirmedAt,
                CanceledAt = source.CanceledAt,
                CancelReason = source.CancelReason,
                CustomerNote = source.CustomerNote,
                CreatedAt = source.CreatedAt,
                UpdatedAt = source.UpdatedAt
            };
        }

        private static AppointmentDetailResponseDto MapAppointment(
            Appointment appointment,
            IReadOnlyDictionary<long, string> serviceNameMap,
            IReadOnlyDictionary<long, string> packageNameMap)
        {
            var response = new AppointmentDetailResponseDto
            {
                Id = appointment.Id,
                BranchId = appointment.BranchId,
                BranchName = appointment.Branch?.Name ?? string.Empty,
                CustomerId = appointment.CustomerId,
                CustomerName = appointment.Customer?.FullName ?? string.Empty,
                CustomerPhone = appointment.Customer?.Phone,
                VehicleId = appointment.VehicleId,
                VehicleLicensePlate = appointment.Vehicle?.LicensePlate,
                VehicleModelName = appointment.Vehicle?.Model?.Brand?.Name != null && appointment.Vehicle?.Model?.ModelName != null
                    ? $"{appointment.Vehicle.Model.Brand.Name} {appointment.Vehicle.Model.ModelName}"
                    : null,
                GroupSessionId = appointment.GroupSessionId,
                GroupSessionCode = appointment.GroupSession?.GroupCode,
                GroupSessionStatus = appointment.GroupSession?.Status,
                CreatedBy = appointment.CreatedBy,
                StartTime = appointment.StartTime,
                EndTime = appointment.EndTime,
                CreatedAt = appointment.CreatedAt,
                RequestedServiceIds = appointment.RequestedServiceIds,
                RequestedPackageIds = appointment.RequestedPackageIds,
                CustomerNotes = appointment.CustomerNotes,
                IsReminderSent = appointment.IsReminderSent,
                Status = appointment.Status,
                CancelReason = appointment.CancelReason,
                UpdatedAt = appointment.UpdatedAt
            };

            foreach (var id in ParseRequestedServiceIds(appointment.RequestedServiceIds))
            {
                response.ServiceNames.Add(serviceNameMap.TryGetValue(id, out var name) ? name : $"#{id}");
            }

            foreach (var id in ParseRequestedServiceIds(appointment.RequestedPackageIds))
            {
                var packageName = packageNameMap.TryGetValue(id, out var name) ? name : $"#{id}";
                response.PackageNames.Add(packageName);
                response.ServiceNames.Add(packageName);
            }

            return response;
        }

        private async Task<IReadOnlyDictionary<long, string>> BuildServiceNameMapAsync(IEnumerable<Appointment> appointments)
        {
            var serviceIds = appointments
                .Where(a => !string.IsNullOrWhiteSpace(a.RequestedServiceIds))
                .SelectMany(a => a.RequestedServiceIds!
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => long.TryParse(s.Trim(), out var parsed) ? parsed : 0L))
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (serviceIds.Count == 0)
            {
                return new Dictionary<long, string>();
            }

            return await _context.StandardServices
                .AsNoTracking()
                .Where(s => serviceIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name ?? $"#{s.Id}");
        }

        private async Task<IReadOnlyDictionary<long, string>> BuildPackageNameMapAsync(IEnumerable<Appointment> appointments)
        {
            var packageIds = appointments
                .Where(a => !string.IsNullOrWhiteSpace(a.RequestedPackageIds))
                .SelectMany(a => a.RequestedPackageIds!
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => long.TryParse(s.Trim(), out var parsed) ? parsed : 0L))
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (packageIds.Count == 0)
            {
                return new Dictionary<long, string>();
            }

            return await _context.Packages
                .AsNoTracking()
                .Where(package => packageIds.Contains(package.Id))
                .ToDictionaryAsync(package => package.Id, package => package.Name ?? $"#{package.Id}");
        }

        private string GenerateGroupSessionCode()
        {
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            return $"BG-{_clock.UtcNow:yyyyMMddHHmmss}-{suffix}";
        }

        private static IEnumerable<long> ParseRequestedServiceIds(string? requestedServiceIds)
        {
            if (string.IsNullOrWhiteSpace(requestedServiceIds))
            {
                return Enumerable.Empty<long>();
            }

            return requestedServiceIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), out var parsed) ? parsed : 0L)
                .Where(v => v > 0)
                .Distinct()
                .ToList();
        }
    }
}
