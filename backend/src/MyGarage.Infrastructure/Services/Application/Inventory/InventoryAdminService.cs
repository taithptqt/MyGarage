using MyGarage.Data;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class InventoryAdminService : IInventoryAdminService
    {
        private readonly IGarageContext _context;

        public InventoryAdminService(IGarageContext context)
        {
            _context = context;
        }
    }
}

