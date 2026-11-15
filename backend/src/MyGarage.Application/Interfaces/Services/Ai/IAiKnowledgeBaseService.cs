using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAiKnowledgeBaseService : IGenericService<AiKnowledgeBase>
    {
        Task<IEnumerable<AiKnowledgeBaseResponseDto>> GetAllDtosAsync();
        Task<AiKnowledgeBaseResponseDto?> GetDtoByIdAsync(long id);
        Task<AiKnowledgeBaseResponseDto> CreateDtoAsync(AiKnowledgeBaseCreateDto itemDto);
        Task<AiKnowledgeBaseResponseDto?> UpdateDtoAsync(long id, AiKnowledgeBaseUpdateDto itemDto);
    }
}
