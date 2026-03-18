using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiSmartDispatchingService : GenericService<AiSmartDispatching>, IAiSmartDispatchingService
    {
        public AiSmartDispatchingService(IAiSmartDispatchingRepository repository) : base(repository)
        {
        }
    }
}
