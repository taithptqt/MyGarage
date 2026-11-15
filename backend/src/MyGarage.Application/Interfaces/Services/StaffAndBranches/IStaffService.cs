using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IStaffService : IGenericService<Staff>
    {
        List<StaffResponseDto> MapToDtos(IEnumerable<Staff> items);
        StaffResponseDto MapToDto(Staff item);
        Task<StaffResponseDto> CreateDtoAsync(StaffCreateDto itemDto, long branchId);
        Task<StaffResponseDto?> UpdateDtoAsync(long id, StaffUpdateDto itemDto, long? scopedBranchId = null);
    }
}
