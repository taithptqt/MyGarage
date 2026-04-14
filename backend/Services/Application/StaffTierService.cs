using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class StaffTierService : GenericService<StaffTier>, IStaffTierService
    {
        public StaffTierService(IStaffTierRepository repository) : base(repository)
        {
        }
    }
}
