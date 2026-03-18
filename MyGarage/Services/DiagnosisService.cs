using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class DiagnosisService : GenericService<Diagnosis>, IDiagnosisService
    {
        public DiagnosisService(IDiagnosisRepository repository) : base(repository)
        {
        }
    }
}
