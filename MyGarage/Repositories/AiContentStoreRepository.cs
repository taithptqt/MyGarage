using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AiContentStoreRepository : GenericRepository<AiContentStore>, IAiContentStoreRepository
    {
        public AiContentStoreRepository(GarageContext context) : base(context)
        {
        }
    }
}
