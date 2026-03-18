using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiKnowledgeBaseService : GenericService<AiKnowledgeBase>, IAiKnowledgeBaseService
    {
        public AiKnowledgeBaseService(IAiKnowledgeBaseRepository repository) : base(repository)
        {
        }
    }
}
