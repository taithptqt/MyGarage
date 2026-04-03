using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class MaterialRequestDetailRepository : GenericRepository<MaterialRequestDetail>, IMaterialRequestDetailRepository
    {
        public MaterialRequestDetailRepository(GarageContext context) : base(context)
        {
        }
    }
}
