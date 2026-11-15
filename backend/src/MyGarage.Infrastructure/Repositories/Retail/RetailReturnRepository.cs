using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class RetailReturnRepository : GenericRepository<RetailReturn>, IRetailReturnRepository
    {
        public RetailReturnRepository(IGarageContext context) : base(context)
        {
        }
    }
}

