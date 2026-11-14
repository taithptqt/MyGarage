using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class InventoryRepository : GenericRepository<Inventory>, IInventoryRepository
    {
        public InventoryRepository(IGarageContext context) : base(context)
        {
        }

        public async Task<Inventory?> GetByProductAndWarehouseAsync(long productId, long warehouseId)
        {
            return await _context.Inventories
                .FirstOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId);
        }
    }
}

