using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class VehicleBrandRepository : GenericRepository<VehicleBrand>, IVehicleBrandRepository
    {
        public VehicleBrandRepository(GarageContext context) : base(context)
        {
        }
    }
}
