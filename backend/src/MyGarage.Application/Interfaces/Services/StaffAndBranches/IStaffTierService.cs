using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IStaffTierService : IGenericService<StaffTier>
    {
        Task<IEnumerable<StaffTierResponseDto>> GetAllDtosAsync();
        Task<StaffTierResponseDto?> GetDtoByIdAsync(long id);
        Task<StaffTierResponseDto> CreateDtoAsync(StaffTierCreateDto itemDto);
        Task<StaffTierResponseDto?> UpdateDtoAsync(long id, StaffTierUpdateDto itemDto);
    }
}
