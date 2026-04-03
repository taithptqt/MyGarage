using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiCustomerInsightService : GenericService<AiCustomerInsight>, IAiCustomerInsightService
    {
        public AiCustomerInsightService(IAiCustomerInsightRepository repository) : base(repository)
        {
        }
    }
}
