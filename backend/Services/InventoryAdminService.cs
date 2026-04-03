using MyGarage.Data;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class InventoryAdminService : IInventoryAdminService
    {
        private readonly GarageContext _context;

        public InventoryAdminService(GarageContext context)
        {
            _context = context;
        }
    }
}
