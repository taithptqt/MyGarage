using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses.Execution;

namespace MyGarage.Interfaces
{
    public interface IBayManagementService
    {
        Task<BayAllocationDto> AllocateBayAsync(BayAllocationCreateDto model, long staffId);
        Task<BayAllocationDto> ReleaseBayAsync(long bayId, long releaseByStaffId, string? notes);
        Task<IEnumerable<BayTimelineDto>> GetBayTimelineAsync(DateTime date, long branchId);
    }
}
