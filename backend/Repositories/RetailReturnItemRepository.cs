using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class RetailReturnItemRepository : GenericRepository<RetailReturnItem>, IRetailReturnItemRepository
    {
        public RetailReturnItemRepository(IGarageContext context) : base(context)
        {
        }
    }
}

