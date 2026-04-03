using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class StaffTierRepository : GenericRepository<StaffTier>, IStaffTierRepository
    {
        public StaffTierRepository(GarageContext context) : base(context)
        {
        }
    }
}
