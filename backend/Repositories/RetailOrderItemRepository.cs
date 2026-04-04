using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class RetailOrderItemRepository : GenericRepository<RetailOrderItem>, IRetailOrderItemRepository
    {
        public RetailOrderItemRepository(IGarageContext context) : base(context)
        {
        }
    }
}

