using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class MaterialRequestDetailService : GenericService<MaterialRequestDetail>, IMaterialRequestDetailService
    {
        public MaterialRequestDetailService(IMaterialRequestDetailRepository repository) : base(repository)
        {
        }
    }
}
