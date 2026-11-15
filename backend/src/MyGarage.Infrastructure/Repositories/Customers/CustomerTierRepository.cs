using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class CustomerTierRepository : GenericRepository<CustomerTier>, ICustomerTierRepository
    {
        public CustomerTierRepository(IGarageContext context) : base(context)
        {
        }
    }
}

