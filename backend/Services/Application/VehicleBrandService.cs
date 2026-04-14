using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class VehicleBrandService : GenericService<VehicleBrand>, IVehicleBrandService
    {
        public VehicleBrandService(IVehicleBrandRepository repository) : base(repository)
        {
        }
    }
}
