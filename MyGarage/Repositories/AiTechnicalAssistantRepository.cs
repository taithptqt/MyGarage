using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AiTechnicalAssistantRepository : GenericRepository<AiTechnicalAssistant>, IAiTechnicalAssistantRepository
    {
        public AiTechnicalAssistantRepository(GarageContext context) : base(context)
        {
        }
    }
}
