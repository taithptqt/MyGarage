using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IAppointmentDispatchOpsService
    {
        Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null);
        Task<BranchBookingHealthResponseDto> GetBranchBookingHealthAsync(long branchId, DateTime? at = null);
        Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null);
        Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null);
        Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null);
        Task<int> ReleaseExpiredLockedSlotsAsync(DateTime? now = null);
    }
}
