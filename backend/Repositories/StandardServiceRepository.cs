using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class StandardServiceRepository : GenericRepository<StandardService>, IStandardServiceRepository
    {
        public StandardServiceRepository(GarageContext context) : base(context)
        {
        }
    }
}
