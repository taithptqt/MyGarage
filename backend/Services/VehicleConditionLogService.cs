using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class VehicleConditionLogService : GenericService<VehicleConditionLog>, IVehicleConditionLogService
    {
        public VehicleConditionLogService(IVehicleConditionLogRepository repository) : base(repository)
        {
        }
    }
}
