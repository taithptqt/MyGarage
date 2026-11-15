using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IShiftService : IGenericService<Shift>
    {
        List<ShiftResponseDto> MapToDtos(IEnumerable<Shift> items);
        Task<ShiftResponseDto?> GetDtoByIdAsync(long id);
        Task<ShiftResponseDto> CreateDtoAsync(ShiftCreateDto itemDto);
        Task<ShiftResponseDto?> UpdateDtoAsync(long id, ShiftUpdateDto itemDto);
    }
}
