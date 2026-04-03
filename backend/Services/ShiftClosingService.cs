using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ShiftClosingService : GenericService<ShiftClosing>, IShiftClosingService
    {
        public ShiftClosingService(IShiftClosingRepository repository) : base(repository)
        {
        }
    }
}
