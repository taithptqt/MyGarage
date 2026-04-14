using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiTechnicalAssistantService : GenericService<AiTechnicalAssistant>, IAiTechnicalAssistantService
    {
        public AiTechnicalAssistantService(IAiTechnicalAssistantRepository repository) : base(repository)
        {
        }
    }
}
