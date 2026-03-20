using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AiModelConfigRepository : GenericRepository<AiModelConfig>, IAiModelConfigRepository
    {
        public AiModelConfigRepository(GarageContext context) : base(context)
        {
        }
    }
}
