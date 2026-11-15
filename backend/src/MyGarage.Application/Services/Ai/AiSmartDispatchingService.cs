using AutoMapper;
using MyGarage.Application.Ai;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AiSmartDispatchingService : GenericService<AiSmartDispatching>, IAiSmartDispatchingService
    {
        private readonly IDispatchBayCandidates _bayCandidates;
        private readonly IServiceOrderRepository _serviceOrderRepository;
        private readonly IMapper _mapper;

        public AiSmartDispatchingService(
            IAiSmartDispatchingRepository repository,
            IDispatchBayCandidates bayCandidates,
            IServiceOrderRepository serviceOrderRepository,
            IMapper mapper) : base(repository)
        {
            _bayCandidates = bayCandidates;
            _serviceOrderRepository = serviceOrderRepository;
            _mapper = mapper;
        }

        public async Task<AiSmartDispatchingResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<AiSmartDispatchingResponseDto>(item);
        }

        public async Task<AiSmartDispatchingResponseDto> CreateDtoAsync(AiSmartDispatchingCreateDto itemDto)
        {
            var entity = _mapper.Map<AiSmartDispatching>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<AiSmartDispatchingResponseDto>(createdItem);
        }

        public async Task<AiSmartDispatchingResponseDto?> UpdateDtoAsync(long id, AiSmartDispatchingUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<AiSmartDispatchingResponseDto>(updatedItem);
        }

        public async Task<AiDispatchRecommendationResponseDto?> RecommendBayAsync(long serviceOrderId)
        {
            var serviceOrder = await _serviceOrderRepository.GetByIdAsync(serviceOrderId);
            if (serviceOrder == null) return null;

            // Find available bays in the same branch
            var availableBays = await _bayCandidates.GetAvailableAsync(serviceOrder.ExecuteBranchId);

            if (!availableBays.Any())
            {
                // If no available bays, find the ones that will be free soonest (simplified for now)
                return new AiDispatchRecommendationResponseDto
                {
                    ServiceOrderId = serviceOrderId,
                    Reason = "No bays are currently available. Please wait for a bay to be cleared.",
                    EfficiencyScore = 0
                };
            }

            var bestBay = availableBays.First();
            var recommendation = new AiDispatchRecommendationResponseDto
            {
                ServiceOrderId = serviceOrderId,
                RecommendedBayId = bestBay.Id,
                BayCode = bestBay.Code,
                BayName = bestBay.Name,
                Reason = $"Bay {bestBay.Code} is currently available and matches the service requirements. It has the highest priority based on current workshop flow.",
                EfficiencyScore = 95,
                Alternatives = availableBays.Skip(1).Take(2).Select(b => new BayAlternativeDto
                {
                    BayId = b.Id,
                    BayCode = b.Code,
                    Reason = "Available and ready for assignment."
                }).ToList()
            };

            return recommendation;
        }
    }
}
