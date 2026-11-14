using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiModelConfigService : GenericService<AiModelConfig>, IAiModelConfigService
    {
        public AiModelConfigService(IAiModelConfigRepository repository) : base(repository)
        {
        }
    }
}
