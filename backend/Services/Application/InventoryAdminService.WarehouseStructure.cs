using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class InventoryAdminService
    {
        public async Task<object> GetWarehouseStructureAsync(int? page = null, int? pageSize = null)
        {
            var query = _context.Warehouses
                .AsNoTracking()
                .Include(w => w.Branch)
                .Where(w => w.IsDeleted != true)
                .OrderBy(w => w.Branch.Name)
                .ThenBy(w => w.Name)
                .Select(w => new WarehouseStructureRow
                {
                    id = w.Id,
                    code = w.Code,
                    name = w.Name,
                    type = w.Type,
                    branchId = w.BranchId,
                    branchName = w.Branch.Name,
                    isActive = w.IsActive,
                    managerId = w.ManagerId
                });

            var hasPaging = page.HasValue || pageSize.HasValue;
            var normalizedPage = page.GetValueOrDefault(1);
            if (normalizedPage < 1) normalizedPage = 1;

            var normalizedPageSize = pageSize.GetValueOrDefault(20);
            if (normalizedPageSize < 1) normalizedPageSize = 1;
            if (normalizedPageSize > 200) normalizedPageSize = 200;

            List<WarehouseStructureRow> warehouses;
            int total;

            if (hasPaging)
            {
                total = await query.CountAsync();
                warehouses = await query
                    .Skip((normalizedPage - 1) * normalizedPageSize)
                    .Take(normalizedPageSize)
                    .ToListAsync();
            }
            else
            {
                warehouses = await query.ToListAsync();
                total = warehouses.Count;
            }

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

            if (!hasPaging)
            {
                return new { warehouses, bins };
            }

            return new
            {
                warehouses,
                bins,
                total,
                page = normalizedPage,
                pageSize = normalizedPageSize,
                totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)normalizedPageSize)
            };
        }

        private sealed class WarehouseStructureRow
        {
            public long id { get; set; }
            public string? code { get; set; }
            public string? name { get; set; }
            public string? type { get; set; }
            public long? branchId { get; set; }
            public string? branchName { get; set; }
            public bool? isActive { get; set; }
            public long? managerId { get; set; }
        }
    }
}
