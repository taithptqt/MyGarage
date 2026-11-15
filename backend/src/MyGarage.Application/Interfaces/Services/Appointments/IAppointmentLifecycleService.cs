using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentLifecycleService
    {
        Task<Appointment?> ConfirmAsync(long id, long? actorUserId = null);
        Task<Appointment?> CancelAsync(long id, string? reason, long? actorUserId = null);
        Task<Appointment?> CheckInAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null);
        Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null);
        Task<WorkSlotDynamicExtensionResponseDto> ProcessDynamicExtensionAsync(
            long workSlotId,
            WorkSlotDynamicExtensionRequestDto dto,
            long? actorUserId = null);
        Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null);
        Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null);
        Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null);
        Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null);
        Task<int> ReleaseExpiredLockedSlotsAsync(DateTime? now = null);
    }
}
