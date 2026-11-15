using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IServiceStaffAssignmentBatchService
    {
        Task<ServiceStaffAssignmentCommandResult<IEnumerable<ServiceStaffAssignmentResponseDto>>> AssignMultiAsync(
            ServiceStaffAssignmentMultiCreateDto request,
            long? scopedBranchId,
            long? currentUserId);

        Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentBulkResponse>> BulkAssignAsync(
            IReadOnlyList<ServiceStaffAssignmentBulkItemInput>? assignments,
            long? scopedBranchId,
            long? currentUserId);
    }
}
