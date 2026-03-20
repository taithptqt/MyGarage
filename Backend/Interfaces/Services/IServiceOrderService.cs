using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Service;
using MyGarage.DTOs.Responses.Service;

namespace MyGarage.Interfaces.Services
{
    public interface IServiceOrderService
    {
        // UC-1.2.1: Initializing ServiceOrder
        Task<FlowServiceOrderResponseDto> CreateServiceOrderAsync(CreateServiceOrderRequestDto request, long createdByStaffId);
        
        // UC-1.2.2: Recording Diagnosis
        Task<FlowDiagnosisResponseDto> AddDiagnosisAsync(CreateDiagnosisRequestDto request);
        
        // UC-1.2.3: Adding Recommendations
        Task<FlowRecommendationResponseDto> AddRecommendationAsync(long diagnosisId, CreateRecommendationRequestDto request);
        
        // UC-1.2.4: Approving Recommendations
        Task<FlowServiceOrderResponseDto> ApproveRecommendationsAsync(long serviceOrderId, ApproveRecommendationsRequestDto request);
        
        // UC-1.2.5: Tracking progress
        Task<FlowServiceOrderResponseDto> UpdateOrderStatusAsync(long serviceOrderId, string status);
        Task<FlowServiceOrderResponseDto> GetServiceOrderByIdAsync(long id);
        Task<IEnumerable<FlowServiceOrderResponseDto>> GetAllServiceOrdersAsync();
    }
}

