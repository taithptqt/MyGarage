using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AiKnowledgeBaseRepository : GenericRepository<AiKnowledgeBase>, IAiKnowledgeBaseRepository
    {
        public AiKnowledgeBaseRepository(IGarageContext context) : base(context)
        {
        }
    }
}

