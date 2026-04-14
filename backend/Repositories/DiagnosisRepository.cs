using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class DiagnosisRepository : GenericRepository<Diagnosis>, IDiagnosisRepository
    {
        public DiagnosisRepository(IGarageContext context) : base(context)
        {
        }
    }
}

