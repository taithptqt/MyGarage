using System;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentCheckInService
    {
        Task EnsurePartsReadyForConfirmationAsync(Appointment appointment, long? actorStaffId);
        Task ApplyArrivalStatusAsync(Appointment appointment, DateTime arriveTime);
        Task EnsureServiceOrderExistsForArrivedAppointmentAsync(
            Appointment appointment,
            DateTime arriveTime,
            long? actorStaffId);
    }
}
