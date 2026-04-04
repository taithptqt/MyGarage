using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class InventoryAdminService
    {
        public async Task<object> GetWarehouseStructureAsync()
        {
            var warehouses = await _context.Warehouses
                .AsNoTracking()
                .Include(w => w.Branch)
                .Where(w => w.IsDeleted != true)
                .OrderBy(w => w.Branch.Name)
                .ThenBy(w => w.Name)
                .Select(w => new
                {
                    id = w.Id,
                    code = w.Code,
                    name = w.Name,
                    type = w.Type,
                    branchId = w.BranchId,
                    branchName = w.Branch.Name,
                    isActive = w.IsActive,
                    managerId = w.ManagerId
                })
                .ToListAsync();

            var bins = await _context.Inventories
                .AsNoTracking()
                .Where(i => i.LocationBin != null && i.LocationBin != "")
                .GroupBy(i => new { i.WarehouseId, i.LocationBin })
                .Select(g => new
                {
                    warehouseId = g.Key.WarehouseId,
                    bin = g.Key.LocationBin!,
                    skuCount = g.Select(x => x.ProductId).Distinct().Count()
                })
                .ToListAsync();

            return new { warehouses, bins };
        }
    }
}
