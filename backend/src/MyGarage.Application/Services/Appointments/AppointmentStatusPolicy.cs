using System;
using MyGarage.Domain.Appointments;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentStatusPolicy : IAppointmentStatusPolicy
    {
        public void EnsureCanConfirm(Appointment appointment)
        {
            if (!AppointmentLifecycleRules.CanConfirm(appointment.Status))
            {
                throw new InvalidOperationException($"Chi co the xac nhan lich hen cho trang thai PENDING/LATE. Hien tai: {appointment.Status}");
            }
        }

        public void EnsureCanCancel(Appointment appointment)
        {
            if (!AppointmentLifecycleRules.CanCancel(appointment.Status))
            {
                throw new InvalidOperationException($"Không thể hủy lịch hẹn ở trạng thái: {appointment.Status}");
            }
        }

        public void EnsureCanCheckIn(Appointment appointment)
        {
            if (!AppointmentLifecycleRules.CanCheckIn(appointment.Status))
            {
                throw new InvalidOperationException($"Chi co the check-in lich o trang thai CONFIRMED/LATE. Hien tai: {appointment.Status}");
            }
        }

        public void EnsureCanArrive(Appointment appointment)
        {
            if (!AppointmentLifecycleRules.CanArrive(appointment.Status))
            {
                throw new InvalidOperationException($"Không thể tiếp nhận lịch ở trạng thái: {appointment.Status}");
            }
        }

        public string? BuildCancelReason(Appointment appointment, string? reason, DateTime now)
        {
            return AppointmentLifecycleRules.BuildCancelReason(appointment.StartTime, reason, now);
        }
    }
}


