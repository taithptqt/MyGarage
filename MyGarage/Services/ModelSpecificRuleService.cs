using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ModelSpecificRuleService : GenericService<ModelSpecificRule>, IModelSpecificRuleService
    {
        public ModelSpecificRuleService(IModelSpecificRuleRepository repository) : base(repository)
        {
        }
    }
}
