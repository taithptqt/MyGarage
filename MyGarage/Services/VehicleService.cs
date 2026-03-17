using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class VehicleService : GenericService<Vehicle>, IVehicleService
    {
        public VehicleService(IVehicleRepository repository) : base(repository)
        {
        }
    }
}
