using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CustomerVehicleService : GenericService<CustomerVehicle>, ICustomerVehicleService
    {
        public CustomerVehicleService(ICustomerVehicleRepository repository) : base(repository)
        {
        }
    }
}
