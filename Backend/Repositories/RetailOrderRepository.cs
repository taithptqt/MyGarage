using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class RetailOrderRepository : GenericRepository<RetailOrder>, IRetailOrderRepository
    {
        public RetailOrderRepository(GarageContext context) : base(context)
        {
        }
    }
}
