using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class DiagnosisRecommendationService : GenericService<DiagnosisRecommendation>, IDiagnosisRecommendationService
    {
        private readonly IMapper _mapper;

        public DiagnosisRecommendationService(IDiagnosisRecommendationRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public IEnumerable<DiagnosisRecommendationResponseDto> MapToDtos(IEnumerable<DiagnosisRecommendation> items)
        {
            return _mapper.Map<IEnumerable<DiagnosisRecommendationResponseDto>>(items);
        }

        public DiagnosisRecommendationResponseDto MapToDto(DiagnosisRecommendation item)
        {
            return _mapper.Map<DiagnosisRecommendationResponseDto>(item);
        }

        public DiagnosisRecommendation MapToEntity(DiagnosisRecommendationCreateDto itemDto)
        {
            return _mapper.Map<DiagnosisRecommendation>(itemDto);
        }

        public void ApplyUpdate(DiagnosisRecommendationUpdateDto itemDto, DiagnosisRecommendation existingItem)
        {
            _mapper.Map(itemDto, existingItem);
        }

        public async Task<DiagnosisRecommendationResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<DiagnosisRecommendationResponseDto>(item);
        }
    }
}
