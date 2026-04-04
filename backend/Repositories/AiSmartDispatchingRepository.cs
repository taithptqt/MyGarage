using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AiSmartDispatchingRepository : GenericRepository<AiSmartDispatching>, IAiSmartDispatchingRepository
    {
        public AiSmartDispatchingRepository(IGarageContext context) : base(context)
        {
        }
    }
}

