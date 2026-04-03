using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class InventoryAdminService
    {
        public async Task<object> GetReportsAsync()
        {
            var inventoryRows = await LoadInventoryRowsForReportAsync();
            var transactions = await LoadTransactionsForReportAsync();
            var stockByBranch = BuildStockByBranch(inventoryRows);
            var topOutbound = BuildTopOutboundProducts(transactions);

            return new
            {
                stockByBranch,
                nearOutOfStock = inventoryRows.Where(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel).Take(50),
                defectiveStocks = inventoryRows.Where(i => i.Warehouse.Type != null && i.Warehouse.Type.ToUpper().Contains("DEFECTIVE")),
                topOutbound
            };
        }

        public async Task<IReadOnlyList<object>> GetInventoryAuditLogAsync(int take)
        {
            take = Math.Clamp(take, 1, 2000);
            var inventoryTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Products",
                "ProductImages",
                "Inventories",
                "StockTransactions",
                "InventoryAudits",
                "InventoryAuditDetails",
                "Warehouses",
                "ApprovalRequests"
            };

            return await _context.AuditLogs
                .AsNoTracking()
                .Where(l => inventoryTables.Contains(l.TableName))
                .OrderByDescending(l => l.Timestamp)
                .Take(take)
                .Select(l => (object)new
                {
                    id = l.Id,
                    timestamp = l.Timestamp,
                    userId = l.UserId,
                    userType = l.UserType,
                    action = l.Action,
                    tableName = l.TableName,
                    recordId = l.RecordId,
                    oldValue = l.OldValue,
                    newValue = l.NewValue
                })
                .ToListAsync();
        }

        private sealed record AlertViewModel(
            string Type,
            string Severity,
            string Message,
            string BranchName,
            string WarehouseName,
            long ReferenceId,
            string ReferenceType,
            DateTime CreatedAt);
    }
}
