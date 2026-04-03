using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IBookingGroupService
    {
        Task<long?> ResolveStaffActorIdAsync(long? actorUserId);
        Task<BookingGroup> CreateAsync(long customerId, long branchId, string? customerNote, long? actorStaffId);
        Task SyncCountersAsync(long? bookingGroupId, long? actorStaffId = null);
        Task<CancelBookingGroupResponseDto?> CancelAllAsync(long bookingGroupId, string reason, long? actorUserId = null);
        Task<IEnumerable<BookingGroupResponseDto>> GetByCustomerAsync(long customerId);
        Task<BookingGroupResponseDto?> GetByCustomerAndIdAsync(long customerId, long bookingGroupId);
        Task<IEnumerable<BookingGroupResponseDto>> GetByBranchAsync(long branchId);
        Task<BookingGroupResponseDto?> GetByBranchAndIdAsync(long branchId, long bookingGroupId);
    }
}
