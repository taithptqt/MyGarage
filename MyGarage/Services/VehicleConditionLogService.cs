using MyGarage.Models;
using MyGarage.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Services
{
    public class VehicleConditionLogService : GenericService<VehicleConditionLog>, IVehicleConditionLogService
    {
        private readonly IVehicleConditionLogRepository _vehicleConditionLogRepository;

        public VehicleConditionLogService(IVehicleConditionLogRepository repository) : base(repository)
        {
            _vehicleConditionLogRepository = repository;
        }

        public async Task<IEnumerable<VehicleConditionLog>> GetByServiceOrderIdAsync(long serviceOrderId)
        {
            return await _vehicleConditionLogRepository.GetByServiceOrderIdAsync(serviceOrderId);
        }
    }
}
