using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InventoryAuditDetailService : GenericService<InventoryAuditDetail>, IInventoryAuditDetailService
    {
        public InventoryAuditDetailService(IInventoryAuditDetailRepository repository) : base(repository)
        {
        }
    }
}
