using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class PurchaseOrderService : GenericService<PurchaseOrder>, IPurchaseOrderService
    {
        public PurchaseOrderService(IPurchaseOrderRepository repository) : base(repository)
        {
        }
    }
}
