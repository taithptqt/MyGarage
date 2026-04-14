using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InventoryService : GenericService<Inventory>, IInventoryService
    {
        public InventoryService(IInventoryRepository repository) : base(repository)
        {
        }
    }
}
