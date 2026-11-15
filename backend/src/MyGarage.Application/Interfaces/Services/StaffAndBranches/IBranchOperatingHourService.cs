using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IBranchOperatingHourService : IGenericService<BranchOperatingHour>
    {
        Task<IEnumerable<BranchOperatingHourResponseDto>> GetAllDtosAsync();
        Task<BranchOperatingHourResponseDto?> GetDtoByIdAsync(long id);
        Task<BranchOperatingHourResponseDto> CreateDtoAsync(BranchOperatingHourCreateDto itemDto);
        Task<BranchOperatingHourResponseDto?> UpdateDtoAsync(long id, BranchOperatingHourUpdateDto itemDto);
    }
}
