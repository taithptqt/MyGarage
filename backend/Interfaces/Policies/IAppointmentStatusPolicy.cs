using System;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentStatusPolicy
    {
        void EnsureCanConfirm(Appointment appointment);
        void EnsureCanCancel(Appointment appointment);
        void EnsureCanCheckIn(Appointment appointment);
        void EnsureCanArrive(Appointment appointment);
        string? BuildCancelReason(Appointment appointment, string? reason, DateTime now);
    }
}
