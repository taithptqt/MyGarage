using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IServiceStaffAssignmentService : IGenericService<ServiceStaffAssignment>
    {
        List<ServiceStaffAssignmentResponseDto> MapToDtos(IEnumerable<ServiceStaffAssignment> items);
        ServiceStaffAssignmentResponseDto MapToDto(ServiceStaffAssignment item);
    }
}
