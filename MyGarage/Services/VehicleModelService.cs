using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class VehicleModelService : GenericService<VehicleModel>, IVehicleModelService
    {
        public VehicleModelService(IVehicleModelRepository repository) : base(repository)
        {
        }
    }
}
