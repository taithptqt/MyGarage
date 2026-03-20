using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class DiagnoseRecommendationRepository : GenericRepository<DiagnoseRecommendation>, IDiagnoseRecommendationRepository
    {
        public DiagnoseRecommendationRepository(GarageContext context) : base(context)
        {
        }
    }
}
