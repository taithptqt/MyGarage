using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AiCustomerInsightRepository : GenericRepository<AiCustomerInsight>, IAiCustomerInsightRepository
    {
        public AiCustomerInsightRepository(GarageContext context) : base(context)
        {
        }
    }
}
