using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class InventoryAuditRepository : GenericRepository<InventoryAudit>, IInventoryAuditRepository
    {
        public InventoryAuditRepository(IGarageContext context) : base(context)
        {
        }
    }
}

