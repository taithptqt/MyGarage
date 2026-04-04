using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiContentStoreService : GenericService<AiContentStore>, IAiContentStoreService
    {
        public AiContentStoreService(IAiContentStoreRepository repository) : base(repository)
        {
        }
    }
}
