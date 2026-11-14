using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class DiagnosisRecommendationRepository : GenericRepository<DiagnosisRecommendation>, IDiagnosisRecommendationRepository
    {
        public DiagnosisRecommendationRepository(IGarageContext context) : base(context)
        {
        }
    }
}

