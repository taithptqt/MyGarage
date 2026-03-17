using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class VehicleModelRepository : GenericRepository<VehicleModel>, IVehicleModelRepository
    {
        public VehicleModelRepository(GarageContext context) : base(context)
        {
        }
    }
}
