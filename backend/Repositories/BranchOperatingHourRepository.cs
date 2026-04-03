using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class BranchOperatingHourRepository : GenericRepository<BranchOperatingHour>, IBranchOperatingHourRepository
    {
        public BranchOperatingHourRepository(GarageContext context) : base(context)
        {
        }
    }
}
