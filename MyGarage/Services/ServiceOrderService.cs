using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceOrderService : GenericService<ServiceOrder>, IServiceOrderService
    {
        private readonly IVehicleConditionLogRepository _logRepository;

        public ServiceOrderService(IServiceOrderRepository repository, IVehicleConditionLogRepository logRepository) : base(repository)
        {
            _logRepository = logRepository;
        }

        public async Task<bool> HasCheckInLogAsync(long serviceOrderId)
        {
            return await _logRepository.ExistsAsync(serviceOrderId, "CHECK_IN");
        }

        public async Task<bool> HasCheckOutLogAsync(long serviceOrderId)
        {
            return await _logRepository.ExistsAsync(serviceOrderId, "CHECK_OUT");
        }
    }
}
