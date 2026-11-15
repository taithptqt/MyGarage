using System;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentCheckInOrderService
    {
        Task EnsureServiceOrderExistsForArrivedAppointmentAsync(
            Appointment appointment,
            DateTime arriveTime,
            long? actorStaffId);
    }
}
