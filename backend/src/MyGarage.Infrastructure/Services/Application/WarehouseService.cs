using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class WarehouseService : GenericService<Warehouse>, IWarehouseService
    {
        public WarehouseService(IWarehouseRepository repository) : base(repository)
        {
        }
    }
}
