using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IStaffScheduleService : IGenericService<StaffSchedule>
    {
        Task<IEnumerable<StaffScheduleResponseDto>> GetAllDtosAsync();
        Task<StaffScheduleResponseDto?> GetDtoByIdAsync(long id);
        Task<StaffScheduleResponseDto> CreateDtoAsync(StaffScheduleCreateDto itemDto);
        Task<StaffScheduleResponseDto?> UpdateDtoAsync(long id, StaffScheduleUpdateDto itemDto);
    }
}
