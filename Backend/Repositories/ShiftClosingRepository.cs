using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ShiftClosingRepository : GenericRepository<ShiftClosing>, IShiftClosingRepository
    {
        public ShiftClosingRepository(GarageContext context) : base(context)
        {
        }
    }
}
