using System;
using MyGarage.Constants;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentStatusPolicy : IAppointmentStatusPolicy
    {
        private const int LateCancelMinutes = 60;

        public void EnsureCanConfirm(Appointment appointment)
        {
            if (!string.Equals(appointment.Status, nameof(AppointmentStatus.PENDING), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, nameof(AppointmentStatus.LATE), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Chi co the xac nhan lich hen cho trang thai PENDING/LATE. Hien tai: {appointment.Status}");
            }
        }

        public void EnsureCanCancel(Appointment appointment)
        {
            if (AppointmentStatusGroups.Closed.Contains(appointment.Status))
            {
                throw new InvalidOperationException($"Không thể hủy lịch hẹn ở trạng thái: {appointment.Status}");
            }
        }

        public void EnsureCanCheckIn(Appointment appointment)
        {
            if (!string.Equals(appointment.Status, nameof(AppointmentStatus.CONFIRMED), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, nameof(AppointmentStatus.LATE), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Chi co the check-in lich o trang thai CONFIRMED/LATE. Hien tai: {appointment.Status}");
            }
        }

        public void EnsureCanArrive(Appointment appointment)
        {
            if (AppointmentStatusGroups.Closed.Contains(appointment.Status))
            {
                throw new InvalidOperationException($"Không thể tiếp nhận lịch ở trạng thái: {appointment.Status}");
            }
        }

        public string? BuildCancelReason(Appointment appointment, string? reason, DateTime now)
        {
            var effectiveReason = reason;

            if (appointment.StartTime > now && (appointment.StartTime - now).TotalMinutes < LateCancelMinutes)
            {
                effectiveReason = string.IsNullOrWhiteSpace(reason) ? "[LATE_CANCEL]" : $"[LATE_CANCEL] {reason}";
            }

            return effectiveReason;
        }
    }
}


