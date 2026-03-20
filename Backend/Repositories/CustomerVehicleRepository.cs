using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class CustomerVehicleRepository : GenericRepository<CustomerVehicle>, ICustomerVehicleRepository
    {
        public CustomerVehicleRepository(GarageContext context) : base(context)
        {
        }
    }
}
