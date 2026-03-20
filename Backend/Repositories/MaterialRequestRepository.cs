using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class MaterialRequestRepository : GenericRepository<MaterialRequest>, IMaterialRequestRepository
    {
        public MaterialRequestRepository(GarageContext context) : base(context)
        {
        }
    }
}
