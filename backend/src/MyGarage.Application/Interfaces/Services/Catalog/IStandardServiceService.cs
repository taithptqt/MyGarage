using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IStandardServiceService : IGenericService<StandardService>
    {
        Task<IEnumerable<StandardServiceResponseDto>> GetAllDtosAsync();
        Task<StandardServiceResponseDto?> GetDtoByIdAsync(long id);
        Task<StandardServiceResponseDto> CreateDtoAsync(StandardServiceCreateDto itemDto);
        Task<StandardServiceResponseDto?> UpdateDtoAsync(long id, StandardServiceUpdateDto itemDto);
    }
}
