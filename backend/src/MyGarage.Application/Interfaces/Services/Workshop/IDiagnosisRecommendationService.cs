using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisRecommendationService : IGenericService<DiagnosisRecommendation>
    {
        IEnumerable<DiagnosisRecommendationResponseDto> MapToDtos(IEnumerable<DiagnosisRecommendation> items);
        DiagnosisRecommendationResponseDto MapToDto(DiagnosisRecommendation item);
        DiagnosisRecommendation MapToEntity(DiagnosisRecommendationCreateDto itemDto);
        void ApplyUpdate(DiagnosisRecommendationUpdateDto itemDto, DiagnosisRecommendation existingItem);
        Task<DiagnosisRecommendationResponseDto?> GetDtoByIdAsync(long id);
    }
}
