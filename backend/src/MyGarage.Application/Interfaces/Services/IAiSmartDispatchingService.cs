using MyGarage.Models;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IAiSmartDispatchingService : IGenericService<AiSmartDispatching>
    {
        Task<AiDispatchRecommendationResponseDto?> RecommendBayAsync(long serviceOrderId);
    }
}
