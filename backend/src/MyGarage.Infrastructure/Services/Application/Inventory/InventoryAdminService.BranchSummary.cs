using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class InventoryAdminService
    {
        public async Task<IEnumerable<object>> GetBranchSummaryAsync()
        {
            var branches = await _context.Branches
                .AsNoTracking()
                .Where(b => b.IsDeleted != true)
                .OrderBy(b => b.Name)
                .Select(g => new
                {
                    branchId = g.Id,
                    branchName = g.Name,
                    branchStatus = g.Status
                })
                .ToListAsync();

            var warehouseCounts = await _context.Warehouses
                .AsNoTracking()
                .Where(w => w.IsDeleted != true)
                .GroupBy(w => w.BranchId)
                .Select(g => new { branchId = g.Key, warehouseCount = g.Count() })
                .ToDictionaryAsync(x => x.branchId, x => x.warehouseCount);

            var inventoryAgg = await _context.Inventories
                .AsNoTracking()
                .Include(i => i.Warehouse)
                .GroupBy(i => i.Warehouse.BranchId)
                .Select(g => new
                {
                    branchId = g.Key,
                    totalQuantity = g.Sum(x => x.Quantity),
                    reservedQuantity = g.Sum(x => x.ReservedQuantity ?? 0),
                    totalValue = g.Sum(x => x.TotalInventoryValue ?? (x.Quantity * (x.CurrentMacPrice ?? 0))),
                    lowStockCount = g.Count(x => x.MinStockLevel.HasValue && x.Quantity <= x.MinStockLevel.Value),
                    negativeStockCount = g.Count(x => x.Quantity < 0)
                })
                .ToDictionaryAsync(x => x.branchId, x => x);

            return branches.Select(b =>
            {
                var hasAgg = inventoryAgg.TryGetValue(b.branchId, out var agg);
                return (object)new
                {
                    b.branchId,
                    b.branchName,
                    b.branchStatus,
                    warehouseCount = warehouseCounts.GetValueOrDefault(b.branchId, 0),
                    totalQuantity = hasAgg ? agg!.totalQuantity : 0m,
                    reservedQuantity = hasAgg ? agg!.reservedQuantity : 0m,
                    totalValue = hasAgg ? agg!.totalValue : 0m,
                    lowStockCount = hasAgg ? agg!.lowStockCount : 0,
                    negativeStockCount = hasAgg ? agg!.negativeStockCount : 0
                };
            }).ToList();
        }
    }
}
