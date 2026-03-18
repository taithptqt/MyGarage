using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiLearningFeedbackService : GenericService<AiLearningFeedback>, IAiLearningFeedbackService
    {
        public AiLearningFeedbackService(IAiLearningFeedbackRepository repository) : base(repository)
        {
        }
    }
}
