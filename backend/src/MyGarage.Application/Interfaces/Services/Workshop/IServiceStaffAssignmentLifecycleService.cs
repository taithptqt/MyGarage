using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IServiceStaffAssignmentLifecycleService
    {
        Task<ServiceStaffAssignmentCommandResult<DTOs.Responses.ServiceStaffAssignmentResponseDto>> StartAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman);

        Task<ServiceStaffAssignmentCommandResult<DTOs.Responses.ServiceStaffAssignmentResponseDto>> PauseAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman);

        Task<ServiceStaffAssignmentCommandResult<DTOs.Responses.ServiceStaffAssignmentResponseDto>> CompleteAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman);
    }
}
