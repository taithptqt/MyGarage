using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAiSmartDispatchingService : IGenericService<AiSmartDispatching>
    {
        Task<AiSmartDispatchingResponseDto?> GetDtoByIdAsync(long id);
        Task<AiSmartDispatchingResponseDto> CreateDtoAsync(AiSmartDispatchingCreateDto itemDto);
        Task<AiSmartDispatchingResponseDto?> UpdateDtoAsync(long id, AiSmartDispatchingUpdateDto itemDto);
        Task<AiDispatchRecommendationResponseDto?> RecommendBayAsync(long serviceOrderId);
    }
}
