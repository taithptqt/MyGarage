using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class PurchaseOrderItemService : GenericService<PurchaseOrderItem>, IPurchaseOrderItemService
    {
        public PurchaseOrderItemService(IPurchaseOrderItemRepository repository) : base(repository)
        {
        }
    }
}
