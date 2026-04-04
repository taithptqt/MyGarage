using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AiCustomerInsightRepository : GenericRepository<AiCustomerInsight>, IAiCustomerInsightRepository
    {
        public AiCustomerInsightRepository(IGarageContext context) : base(context)
        {
        }
    }
}

