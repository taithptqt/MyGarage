using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class VehicleInsuranceService : GenericService<VehicleInsurance>, IVehicleInsuranceService
    {
        public VehicleInsuranceService(IVehicleInsuranceRepository repository) : base(repository)
        {
        }
    }
}
