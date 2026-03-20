using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class MaterialRequestService : GenericService<MaterialRequest>, IMaterialRequestService
    {
        public MaterialRequestService(IMaterialRequestRepository repository) : base(repository)
        {
        }
    }
}
