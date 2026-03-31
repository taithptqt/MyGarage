using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.Data;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class BookingGroupService : IBookingGroupService
    {
        private readonly GarageContext _context;
        private readonly IBookingGroupStatusPolicy _bookingGroupStatusPolicy;
        private readonly ISystemClock _clock;

        public BookingGroupService(
            GarageContext context,
            IBookingGroupStatusPolicy bookingGroupStatusPolicy,
            ISystemClock clock)
        {
            _context = context;
            _bookingGroupStatusPolicy = bookingGroupStatusPolicy;
            _clock = clock;
        }

        public async Task<long?> ResolveStaffActorIdAsync(long? actorUserId)
        {
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return null;
            }

            var staffExists = await _context.Staffs
                .AsNoTracking()
                .AnyAsync(s => s.Id == actorUserId.Value);

            return staffExists ? actorUserId.Value : null;
        }

        public async Task<BookingGroup> CreateAsync(long customerId, long branchId, string? customerNote, long? actorStaffId)
        {
            var entity = new BookingGroup
            {
                GroupCode = GenerateBookingGroupCode(),
                CustomerId = customerId,
                BranchId = branchId,
                Status = BookingGroupStatuses.Pending,
                TotalAppointments = 0,
                ConfirmedAppointments = 0,
                CanceledAppointments = 0,
                CustomerNote = string.IsNullOrWhiteSpace(customerNote) ? null : customerNote.Trim(),
                CreatedAt = _clock.UtcNow,
                UpdatedAt = _clock.UtcNow,
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId
            };

            _context.BookingGroups.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task SyncCountersAsync(long? bookingGroupId, long? actorStaffId = null)
        {
            if (!bookingGroupId.HasValue)
            {
                return;
            }

            var group = await _context.BookingGroups
                .FirstOrDefaultAsync(x => x.Id == bookingGroupId.Value);
            if (group == null)
            {
                return;
            }

            var statuses = await _context.Appointments
                .AsNoTracking()
                .Where(x => x.BookingGroupId == bookingGroupId.Value)
                .Select(x => x.Status)
                .ToListAsync();

            var outcome = _bookingGroupStatusPolicy.Evaluate(statuses, group.Status);
            group.Status = outcome.Status;
            group.TotalAppointments = outcome.TotalAppointments;
            group.ConfirmedAppointments = outcome.ConfirmedAppointments;
            group.CanceledAppointments = outcome.CanceledAppointments;

            var nowUtc = _clock.UtcNow;
            if (outcome.ShouldSetConfirmedAt)
            {
                group.ConfirmedAt = nowUtc;
            }
            if (outcome.ShouldSetCanceledAt)
            {
                group.CanceledAt = nowUtc;
            }
            if (outcome.ShouldClearConfirmedAt)
            {
                group.ConfirmedAt = null;
            }
            if (outcome.ShouldClearCanceledAt)
            {
                group.CanceledAt = null;
            }

            if (actorStaffId.HasValue)
            {
                group.UpdatedBy = actorStaffId.Value;
            }
            group.UpdatedAt = nowUtc;
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<BookingGroupResponseDto>> GetByCustomerAsync(long customerId)
        {
            var groups = await _context.BookingGroups
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return groups.Select(MapHeaderOnly);
        }

        public async Task<BookingGroupResponseDto?> GetByCustomerAndIdAsync(long customerId, long bookingGroupId)
        {
            var group = await _context.BookingGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == bookingGroupId && x.CustomerId == customerId);
            if (group == null)
            {
                return null;
            }

            var appointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Branch)
                .Include(a => a.Customer)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .Where(a => a.BookingGroupId == bookingGroupId)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            var serviceNameMap = await BuildServiceNameMapAsync(appointments);
            var dto = MapHeaderOnly(group);
            dto.Appointments = appointments.Select(a => MapAppointment(a, serviceNameMap)).ToList();
            return dto;
        }

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

            if (!string.IsNullOrWhiteSpace(appointment.RequestedServiceIds))
            {
                var ids = appointment.RequestedServiceIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => long.TryParse(s.Trim(), out var parsed) ? parsed : 0L)
                    .Where(v => v > 0)
                    .Distinct()
                    .ToList();

                foreach (var id in ids)
                {
                    response.ServiceNames.Add(serviceNameMap.TryGetValue(id, out var name) ? name : $"#{id}");
                }
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
    }
}
