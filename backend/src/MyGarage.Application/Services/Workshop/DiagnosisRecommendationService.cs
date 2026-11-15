using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class DiagnosisRecommendationService : GenericService<DiagnosisRecommendation>, IDiagnosisRecommendationService
    {
        public DiagnosisRecommendationService(IDiagnosisRecommendationRepository repository) : base(repository)
        {
        }
    }
}
