using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IShiftClosingService : IGenericService<ShiftClosing>
    {
        List<ShiftClosingResponseDto> MapToDtos(IEnumerable<ShiftClosing> items);
        Task<ShiftClosingResponseDto?> GetDtoByIdAsync(long id);
        Task<ShiftClosingResponseDto> CreateDtoAsync(ShiftClosingCreateDto itemDto);
        Task<ShiftClosingResponseDto?> UpdateDtoAsync(long id, ShiftClosingUpdateDto itemDto);
    }
}
