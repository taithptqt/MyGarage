using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ShiftService : GenericService<Shift>, IShiftService
    {
        public ShiftService(IShiftRepository repository) : base(repository)
        {
        }
    }
}
