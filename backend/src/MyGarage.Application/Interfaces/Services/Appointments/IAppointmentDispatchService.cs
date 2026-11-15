using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IAppointmentDispatchService
    {
        Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null);
        Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null);
        Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null);
        Task<int> ReleaseExpiredLockedSlotsAsync(DateTime? now = null);
    }
}
