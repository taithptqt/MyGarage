using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class DiagnoseRecommendationService : GenericService<DiagnoseRecommendation>, IDiagnoseRecommendationService
    {
        public DiagnoseRecommendationService(IDiagnoseRecommendationRepository repository) : base(repository)
        {
        }
    }
}
