using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class BranchOperatingHourService : GenericService<BranchOperatingHour>, IBranchOperatingHourService
    {
        public BranchOperatingHourService(IBranchOperatingHourRepository repository) : base(repository)
        {
        }
    }
}
