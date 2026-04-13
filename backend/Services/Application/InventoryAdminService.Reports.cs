using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;

namespace MyGarage.Services
{
    public partial class InventoryAdminService
    {
        public async Task<object> GetReportsAsync(int? page = null, int? pageSize = null)
        {
            var inventoryRows = await LoadInventoryRowsForReportAsync();
            var transactions = await LoadTransactionsForReportAsync();
            var stockByBranch = BuildStockByBranch(inventoryRows);
            var topOutbound = BuildTopOutboundProducts(transactions);
            var nearOutOfStock = inventoryRows
                .Where(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel)
                .Take(50)
                .Cast<object>()
                .ToList();
            var defectiveStocks = inventoryRows
                .Where(i => i.Warehouse.Type != null && i.Warehouse.Type.ToUpper().Contains("DEFECTIVE"))
                .Cast<object>()
                .ToList();

            var hasPaging = page.HasValue || pageSize.HasValue;
            if (!hasPaging)
            {
                return new
                {
                    stockByBranch,
                    nearOutOfStock,
                    defectiveStocks,
                    topOutbound
                };
            }

            var normalizedPage = page.GetValueOrDefault(1);
            if (normalizedPage < 1) normalizedPage = 1;
            var normalizedPageSize = pageSize.GetValueOrDefault(20);
            if (normalizedPageSize < 1) normalizedPageSize = 1;
            if (normalizedPageSize > 200) normalizedPageSize = 200;

            return new
            {
                stockByBranch = ToPaged(stockByBranch, normalizedPage, normalizedPageSize),
                nearOutOfStock = ToPaged(nearOutOfStock, normalizedPage, normalizedPageSize),
                defectiveStocks = ToPaged(defectiveStocks, normalizedPage, normalizedPageSize),
                topOutbound = ToPaged(topOutbound, normalizedPage, normalizedPageSize)
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

            var logs = await _context.AuditLogs
                .AsNoTracking()
                .Where(l => inventoryTables.Contains(l.TableName))
                .OrderByDescending(l => l.Timestamp)
                .Take(take)
                .ToListAsync();

            return logs
                .Select(l => (object)new
                {
                    id = l.Id,
                    timestamp = l.Timestamp,
                    userId = l.UserId,
                    userType = Utf8MojibakeRepair.Fix(l.UserType),
                    action = Utf8MojibakeRepair.Fix(l.Action),
                    tableName = Utf8MojibakeRepair.Fix(l.TableName),
                    recordId = l.RecordId,
                    oldValue = Utf8MojibakeRepair.Fix(l.OldValue),
                    newValue = Utf8MojibakeRepair.Fix(l.NewValue)
                })
                .ToList();
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

        private static object ToPaged(IReadOnlyList<object> source, int page, int pageSize)
        {
            var total = source.Count;
            var items = source
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new
            {
                items,
                total,
                page,
                pageSize,
                totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
            };
        }
    }
}
