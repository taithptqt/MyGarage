using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class StaffScheduleRepository : GenericRepository<StaffSchedule>, IStaffScheduleRepository
    {
        public StaffScheduleRepository(GarageContext context) : base(context)
        {
        }
    }
}
