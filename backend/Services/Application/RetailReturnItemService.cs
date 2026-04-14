using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RetailReturnItemService : GenericService<RetailReturnItem>, IRetailReturnItemService
    {
        public RetailReturnItemService(IRetailReturnItemRepository repository) : base(repository)
        {
        }
    }
}
