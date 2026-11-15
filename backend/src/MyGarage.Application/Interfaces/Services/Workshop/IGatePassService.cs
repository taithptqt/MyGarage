using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IGatePassService : IGenericService<GatePass>
    {
        Task<GatePassResponseDto?> GetDtoByIdAsync(long id);
        Task<GatePassResponseDto> CreateDtoAsync(GatePassCreateDto itemDto);
        Task<GatePassResponseDto?> UpdateDtoAsync(long id, GatePassUpdateDto itemDto);
    }
}
