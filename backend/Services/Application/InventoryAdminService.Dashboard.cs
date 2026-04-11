using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public partial class InventoryAdminService
    {
        private const string ApprovalRequestStatusPending = nameof(ApprovalRequestStatusCode.PENDING);
        private const string InventoryAuditStatusDraft = nameof(InventoryAuditStatusCode.DRAFT);
        private const string InventoryAuditStatusReviewing = nameof(InventoryAuditStatusCode.REVIEWING);

        public async Task<object> GetOverviewAsync()
        {
            var now = VietnamTime.Now;
            var staleCutoff = now.AddDays(-90);

            var inventoryRows = await _context.Inventories
                .AsNoTracking()
                .Include(i => i.Product)
                .Include(i => i.Warehouse)
                    .ThenInclude(w => w.Branch)
                .ToListAsync();

            var transactions = await _context.StockTransactions
                .AsNoTracking()
                .ToListAsync();

            var pendingApprovals = await _context.ApprovalRequests
                .AsNoTracking()
                .CountAsync(a => a.Status != null && a.Status.ToUpper() == ApprovalRequestStatusPending);

            var pendingAudits = await _context.InventoryAudits
                .AsNoTracking()
                .CountAsync(a => a.Status != null && (a.Status.ToUpper() == InventoryAuditStatusDraft || a.Status.ToUpper() == InventoryAuditStatusReviewing));

            var lastTransactionByProduct = transactions
                .Where(t => t.ProductId > 0)
                .GroupBy(t => t.ProductId)
                .ToDictionary(g => g.Key, g => g.Max(t => t.CreatedAt ?? DateTime.MinValue));

            var staleProducts = inventoryRows
                .Where(i => i.Quantity > 0)
                .GroupBy(i => i.ProductId)
                .Count(g => !lastTransactionByProduct.TryGetValue(g.Key, out var lastTx) || lastTx < staleCutoff);

            var totalValue = inventoryRows.Sum(i => i.TotalInventoryValue ?? (i.Quantity * (i.CurrentMacPrice ?? i.Product.CostPrice ?? 0)));

            return new
            {
                branchCount = inventoryRows.Select(i => i.Warehouse.BranchId).Distinct().Count(),
                warehouseCount = inventoryRows.Select(i => i.WarehouseId).Distinct().Count(),
                totalQuantity = inventoryRows.Sum(i => i.Quantity),
                reservedQuantity = inventoryRows.Sum(i => i.ReservedQuantity ?? 0),
                lowStockCount = inventoryRows.Count(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel.Value),
                negativeStockCount = inventoryRows.Count(i => i.Quantity < 0),
                defectiveStockCount = inventoryRows.Count(i => i.Warehouse.Type != null && i.Warehouse.Type.ToUpper().Contains("DEFECTIVE")),
                pendingApprovals,
                pendingAudits,
                staleProducts,
                totalInventoryValue = totalValue
            };
        }
    }
}
