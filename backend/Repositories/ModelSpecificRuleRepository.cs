using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ModelSpecificRuleRepository : GenericRepository<ModelSpecificRule>, IModelSpecificRuleRepository
    {
        public ModelSpecificRuleRepository(GarageContext context) : base(context)
        {
        }
    }
}
