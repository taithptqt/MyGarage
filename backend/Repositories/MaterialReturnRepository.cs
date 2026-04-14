using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class MaterialReturnRepository : GenericRepository<MaterialReturn>, IMaterialReturnRepository
    {
        public MaterialReturnRepository(IGarageContext context) : base(context)
        {
        }
    }
}

