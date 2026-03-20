using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class StaffScheduleService : GenericService<StaffSchedule>, IStaffScheduleService
    {
        public StaffScheduleService(IStaffScheduleRepository repository) : base(repository)
        {
        }
    }
}
