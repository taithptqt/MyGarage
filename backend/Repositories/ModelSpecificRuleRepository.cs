using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ModelSpecificRuleRepository : GenericRepository<ModelSpecificRule>, IModelSpecificRuleRepository
    {
        public ModelSpecificRuleRepository(IGarageContext context) : base(context)
        {
        }
    }
}

