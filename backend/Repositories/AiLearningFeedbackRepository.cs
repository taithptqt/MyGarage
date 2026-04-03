using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AiLearningFeedbackRepository : GenericRepository<AiLearningFeedback>, IAiLearningFeedbackRepository
    {
        public AiLearningFeedbackRepository(GarageContext context) : base(context)
        {
        }
    }
}
