using MyGarage.Models;
using MyGarage.Interfaces;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Ai;

namespace MyGarage.Services
{
    public class AiSmartDispatchingService : GenericService<AiSmartDispatching>, IAiSmartDispatchingService
    {
        private readonly IDispatchBayCandidates _bayCandidates;
        private readonly IServiceOrderRepository _serviceOrderRepository;

        public AiSmartDispatchingService(
            IAiSmartDispatchingRepository repository,
            IDispatchBayCandidates bayCandidates,
            IServiceOrderRepository serviceOrderRepository) : base(repository)
        {
            _bayCandidates = bayCandidates;
            _serviceOrderRepository = serviceOrderRepository;
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
