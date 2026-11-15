using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IGroupSessionService
    {
        Task<long?> ResolveStaffActorIdAsync(long? actorUserId);
        Task<GroupSession> CreateAsync(long customerId, long branchId, string? customerNote, long? actorStaffId);
        Task SyncCountersAsync(long? groupSessionId, long? actorStaffId = null);
        Task<CancelGroupSessionResponseDto?> CancelAllAsync(long groupSessionId, string reason, long? actorUserId = null);
        Task<IEnumerable<GroupSessionResponseDto>> GetByCustomerAsync(long customerId);
        Task<GroupSessionResponseDto?> GetByCustomerAndIdAsync(long customerId, long groupSessionId);
        Task<IEnumerable<GroupSessionResponseDto>> GetByBranchAsync(long branchId);
        Task<GroupSessionResponseDto?> GetByBranchAndIdAsync(long branchId, long groupSessionId);
    }
}
