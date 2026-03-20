using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CustomerTierService : GenericService<CustomerTier>, ICustomerTierService
    {
        public CustomerTierService(ICustomerTierRepository repository) : base(repository)
        {
        }
    }
}
