using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class BookingGroupService
    {
        private static BookingGroupResponseDto MapHeaderOnly(BookingGroup source)
        {
            return new BookingGroupResponseDto
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

        private static AppointmentDetailResponseDto MapAppointment(Appointment appointment, IReadOnlyDictionary<long, string> serviceNameMap)
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
                BookingGroupId = appointment.BookingGroupId,
                BookingGroupCode = appointment.BookingGroup?.GroupCode,
                BookingGroupStatus = appointment.BookingGroup?.Status,
                CreatedBy = appointment.CreatedBy,
                StartTime = appointment.StartTime,
                EndTime = appointment.EndTime,
                CreatedAt = appointment.CreatedAt,
                RequestedServiceIds = appointment.RequestedServiceIds,
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

        private string GenerateBookingGroupCode()
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
