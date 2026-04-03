using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RetailOrderItemService : GenericService<RetailOrderItem>, IRetailOrderItemService
    {
        public RetailOrderItemService(IRetailOrderItemRepository repository) : base(repository)
        {
        }
    }
}
