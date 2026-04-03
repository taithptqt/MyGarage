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
            AppointmentStatuses.Pending,
            AppointmentStatuses.Late,
            AppointmentStatuses.Confirmed,
            AppointmentStatuses.Arrived,
            AppointmentStatuses.ArrivedLate
        };

        private static readonly HashSet<string> ConfirmedLikeAppointmentStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            AppointmentStatuses.Confirmed,
            AppointmentStatuses.Arrived,
            AppointmentStatuses.ArrivedLate
        };

        private static readonly HashSet<string> NotStartedServiceOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "NEW",
            "RECEPTION",
            "CANCELED",
            "CANCELLED"
        };

        public async Task<CancelBookingGroupResponseDto?> CancelAllAsync(long bookingGroupId, string reason, long? actorUserId = null)
        {
            var trimmedReason = (reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmedReason))
            {
                throw new InvalidOperationException("Vui lòng nhập lý do hủy nhóm.");
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
                throw new InvalidOperationException("Nhóm này không có lịch hẹn để hủy.");
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

                appointment.Status = AppointmentStatuses.Canceled;
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
                Title = "Cập nhật lịch hẹn nhóm",
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
                return "Do một số xe trong nhóm không xác định được trạng thái và đã quá giờ hẹn, gara xin phép hủy các xe chưa đến trong nhóm của bạn để điều phối cho xe khác. Vui lòng liên hệ hotline để hỗ trợ.";
            }

            return "Do một số xe trong nhóm không xác định được trạng thái và đã quá giờ hẹn, gara xin phép hủy toàn bộ lịch hẹn nhóm của bạn để điều phối cho xe khác. Vui lòng liên hệ hotline để hỗ trợ.";
        }

        private sealed class ServiceOrderStatusSnapshot
        {
            public long AppointmentId { get; set; }
            public string? Status { get; set; }
        }
    }
}
