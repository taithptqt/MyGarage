using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InventoryAuditService : GenericService<InventoryAudit>, IInventoryAuditService
    {
        public InventoryAuditService(IInventoryAuditRepository repository) : base(repository)
        {
        }
    }
}
