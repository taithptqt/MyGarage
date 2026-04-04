using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class BookingGroupService
    {
        private static readonly HashSet<string> GroupCancelableAppointmentStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(AppointmentStatus.PENDING),
            nameof(AppointmentStatus.LATE),
            nameof(AppointmentStatus.CONFIRMED),
            nameof(AppointmentStatus.ARRIVED),
            nameof(AppointmentStatus.ARRIVED_LATE)
        };

        private static readonly HashSet<string> ConfirmedLikeAppointmentStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(AppointmentStatus.CONFIRMED),
            nameof(AppointmentStatus.ARRIVED),
            nameof(AppointmentStatus.ARRIVED_LATE)
        };

        private static readonly HashSet<string> NotStartedServiceOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ServiceOrderLifecycleStatus.NEW),
            nameof(ServiceOrderLifecycleStatus.RECEPTION),
            nameof(ServiceOrderLifecycleStatus.CANCELED),
            nameof(ServiceOrderLifecycleStatus.CANCELLED)
        };

        public async Task<CancelBookingGroupResponseDto?> CancelAllAsync(long bookingGroupId, string reason, long? actorUserId = null)
        {
            var trimmedReason = (reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmedReason))
            {
                throw new InvalidOperationException("Vui l\u00F2ng nh\u1EADp l\u00FD do h\u1EE7y nh\u00F3m.");
            }

            var actorStaffId = await ResolveStaffActorIdAsync(actorUserId);
            var group = await _context.BookingGroups
                .FirstOrDefaultAsync(x => x.Id == bookingGroupId);
            if (group == null)
            {
                return null;
            }

            var appointments = await _context.Appointments
                .Where(a => a.BookingGroupId == bookingGroupId)
                .Include(a => a.Vehicle)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            if (appointments.Count == 0)
            {
                throw new InvalidOperationException("Nh\u00F3m n\u00E0y kh\u00F4ng c\u00F3 l\u1ECBch h\u1EB9n \u0111\u1EC3 h\u1EE7y.");
            }

            var response = new CancelBookingGroupResponseDto
            {
                BookingGroupId = bookingGroupId,
                TotalAppointments = appointments.Count,
                ConfirmedVehiclePlates = appointments
                    .Where(a => ConfirmedLikeAppointmentStatuses.Contains(a.Status))
                    .Select(a => NormalizePlate(a.Vehicle?.LicensePlate))
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            var appointmentIds = appointments
                .Select(a => a.Id)
                .ToList();

            var serviceOrderRows = await _context.ServiceOrders
                .AsNoTracking()
                .Where(x => x.AppointmentId.HasValue && appointmentIds.Contains(x.AppointmentId.Value))
                .Select(x => new ServiceOrderStatusSnapshot
                {
                    AppointmentId = x.AppointmentId!.Value,
                    Status = x.Status
                })
                .ToListAsync();

            var serviceOrderStatusMap = serviceOrderRows
                .GroupBy(x => x.AppointmentId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<string>)g
                        .Select(x => NormalizeStatus(x.Status))
                        .ToList());

            var nowUtc = _clock.UtcNow;

            foreach (var appointment in appointments)
            {
                if (AppointmentStatusGroups.Closed.Contains(appointment.Status))
                {
                    continue;
                }

                if (HasStartedServiceOrder(appointment.Id, serviceOrderStatusMap))
                {
                    response.SkippedStartedAppointmentIds.Add(appointment.Id);
                    var plate = NormalizePlate(appointment.Vehicle?.LicensePlate);
                    if (!string.IsNullOrWhiteSpace(plate))
                    {
                        response.SkippedStartedVehiclePlates.Add(plate);
                    }
                    continue;
                }

                if (!GroupCancelableAppointmentStatuses.Contains(appointment.Status))
                {
                    continue;
                }

                appointment.Status = nameof(AppointmentStatus.CANCELED);
                appointment.CancelReason = trimmedReason;
                appointment.UpdatedAt = nowUtc;
                if (actorStaffId.HasValue)
                {
                    appointment.UpdatedBy = actorStaffId.Value;
                }

                response.CanceledAppointmentIds.Add(appointment.Id);
            }

            response.CanceledCount = response.CanceledAppointmentIds.Count;
            response.SkippedStartedCount = response.SkippedStartedAppointmentIds.Count;
            response.SkippedStartedVehiclePlates = response.SkippedStartedVehiclePlates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (response.CanceledCount <= 0)
            {
                response.GroupStatus = group.Status;
                return response;
            }

            group.CancelReason = trimmedReason;
            group.UpdatedAt = nowUtc;
            if (actorStaffId.HasValue)
            {
                group.UpdatedBy = actorStaffId.Value;
            }

            _context.Notifications.Add(new Notification
            {
                CustomerId = group.CustomerId,
                Title = "C\u1EADp nh\u1EADt l\u1ECBch h\u1EB9n nh\u00F3m",
                Content = BuildGroupCancelNotificationContent(response.SkippedStartedCount > 0),
                IsRead = false,
                SentAt = nowUtc,
                LinkAction = $"/booking-groups/{group.Id}"
            });

            await _context.SaveChangesAsync();
            await SyncCountersAsync(group.Id, actorStaffId);
            await _context.Entry(group).ReloadAsync();

            response.GroupStatus = group.Status;
            return response;
        }

        private static bool HasStartedServiceOrder(
            long appointmentId,
            IReadOnlyDictionary<long, IReadOnlyCollection<string>> serviceOrderStatusMap)
        {
            if (!serviceOrderStatusMap.TryGetValue(appointmentId, out var statuses) || statuses.Count == 0)
            {
                return false;
            }

            return statuses.Any(status => string.IsNullOrWhiteSpace(status) || !NotStartedServiceOrderStatuses.Contains(status));
        }

        private static string NormalizeStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status)
                ? string.Empty
                : status.Trim().ToUpperInvariant();
        }

        private static string NormalizePlate(string? plate)
        {
            return string.IsNullOrWhiteSpace(plate)
                ? string.Empty
                : plate.Trim().ToUpperInvariant();
        }

        private static string BuildGroupCancelNotificationContent(bool isPartialCancellation)
        {
            if (isPartialCancellation)
            {
                return "Do m\u1ED9t s\u1ED1 xe trong nh\u00F3m kh\u00F4ng x\u00E1c \u0111\u1ECBnh \u0111\u01B0\u1EE3c tr\u1EA1ng th\u00E1i v\u00E0 \u0111\u00E3 qu\u00E1 gi\u1EDD h\u1EB9n, gara xin ph\u00E9p h\u1EE7y c\u00E1c xe ch\u01B0a \u0111\u1EBFn trong nh\u00F3m c\u1EE7a b\u1EA1n \u0111\u1EC3 \u0111i\u1EC1u ph\u1ED1i cho xe kh\u00E1c. Vui l\u00F2ng li\u00EAn h\u1EC7 hotline \u0111\u1EC3 h\u1ED7 tr\u1EE3.";
            }

            return "Do m\u1ED9t s\u1ED1 xe trong nh\u00F3m kh\u00F4ng x\u00E1c \u0111\u1ECBnh \u0111\u01B0\u1EE3c tr\u1EA1ng th\u00E1i v\u00E0 \u0111\u00E3 qu\u00E1 gi\u1EDD h\u1EB9n, gara xin ph\u00E9p h\u1EE7y to\u00E0n b\u1ED9 l\u1ECBch h\u1EB9n nh\u00F3m c\u1EE7a b\u1EA1n \u0111\u1EC3 \u0111i\u1EC1u ph\u1ED1i cho xe kh\u00E1c. Vui l\u00F2ng li\u00EAn h\u1EC7 hotline \u0111\u1EC3 h\u1ED7 tr\u1EE3.";
        }

        private sealed class ServiceOrderStatusSnapshot
        {
            public long AppointmentId { get; set; }
            public string? Status { get; set; }
        }
    }
}
