using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class InventoryAuditDetailRepository : GenericRepository<InventoryAuditDetail>, IInventoryAuditDetailRepository
    {
        public InventoryAuditDetailRepository(IGarageContext context) : base(context)
        {
        }
    }
}

