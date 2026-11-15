using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentLifecycleCommandService
    {
        Task<Appointment?> ConfirmAsync(long id, long? actorUserId = null);
        Task<Appointment?> CancelAsync(long id, string? reason, long? actorUserId = null);
        Task<Appointment?> CheckInAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null);
        Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null);
        Task<AppointmentEditCapabilityResponseDto?> GetEditCapabilityAsync(long id);
    }
}
