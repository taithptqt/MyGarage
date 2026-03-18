using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class VehicleConditionLogRepository : GenericRepository<VehicleConditionLog>, IVehicleConditionLogRepository
    {
        public VehicleConditionLogRepository(GarageContext context) : base(context)
        {
        }
    }
}
