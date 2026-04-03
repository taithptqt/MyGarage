using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class VehicleInsuranceRepository : GenericRepository<VehicleInsurance>, IVehicleInsuranceRepository
    {
        public VehicleInsuranceRepository(GarageContext context) : base(context)
        {
        }
    }
}
