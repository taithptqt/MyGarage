using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAiModelConfigService : IGenericService<AiModelConfig>
    {
        Task<IEnumerable<AiModelConfigResponseDto>> GetAllDtosAsync();
        Task<AiModelConfigResponseDto?> GetDtoByIdAsync(long id);
        Task<AiModelConfigResponseDto> CreateDtoAsync(AiModelConfigCreateDto itemDto);
        Task<AiModelConfigResponseDto?> UpdateDtoAsync(long id, AiModelConfigUpdateDto itemDto);
    }
}
