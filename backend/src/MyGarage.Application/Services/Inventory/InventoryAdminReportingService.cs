using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Inventory;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class InventoryAdminReportingService : IInventoryAdminReportingService
    {
        private const string ApprovalRequestStatusPending = nameof(ApprovalRequestStatusCode.PENDING);
        private const string InventoryAuditStatusDraft = nameof(InventoryAuditStatusCode.DRAFT);
        private const string InventoryAuditStatusReviewing = nameof(InventoryAuditStatusCode.REVIEWING);

        private readonly IInventoryStore _store;

        public InventoryAdminReportingService(IInventoryStore store)
        {
            _store = store;
        }

        public async Task<object> GetOverviewAsync()
        {
            var now = VietnamTime.Now;
            var staleCutoff = now.AddDays(-90);

            var inventoryRows = await _store.GetInventoryRowsForReportAsync();
            var transactions = await _store.GetTransactionsForReportAsync();
            var pendingApprovals = await _store.CountPendingApprovalsAsync(ApprovalRequestStatusPending);
            var pendingAudits = await _store.CountPendingAuditsAsync(InventoryAuditStatusDraft, InventoryAuditStatusReviewing);

            var lastTransactionByProduct = transactions
                .Where(t => t.ProductId > 0)
                .GroupBy(t => t.ProductId)
                .ToDictionary(g => g.Key, g => g.Max(t => t.CreatedAt ?? DateTime.MinValue));

            var staleProducts = inventoryRows
                .Where(i => i.Quantity > 0)
                .GroupBy(i => i.ProductId)
                .Count(g => !lastTransactionByProduct.TryGetValue(g.Key, out var lastTx) || lastTx < staleCutoff);

            var totalValue = inventoryRows.Sum(ComputeInventoryValue);

            return new
            {
                branchCount = inventoryRows.Select(i => i.Warehouse.BranchId).Distinct().Count(),
                warehouseCount = inventoryRows.Select(i => i.WarehouseId).Distinct().Count(),
                totalQuantity = inventoryRows.Sum(i => i.Quantity),
                reservedQuantity = inventoryRows.Sum(i => i.ReservedQuantity ?? 0),
                lowStockCount = inventoryRows.Count(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel.Value),
                negativeStockCount = inventoryRows.Count(i => i.Quantity < 0),
                defectiveStockCount = inventoryRows.Count(IsDefectiveWarehouse),
                pendingApprovals,
                pendingAudits,
                staleProducts,
                totalInventoryValue = totalValue
            };
        }

        public async Task<IEnumerable<object>> GetBranchSummaryAsync()
        {
            var branches = await _store.GetActiveBranchesForSummaryAsync();
            var warehouseCounts = await _store.GetActiveWarehouseCountsByBranchAsync();
            var inventoryAgg = await _store.GetBranchInventoryAggregatesAsync();

            return branches.Select(b =>
            {
                var hasAgg = inventoryAgg.TryGetValue(b.BranchId, out var agg);
                return (object)new
                {
                    branchId = b.BranchId,
                    branchName = b.BranchName,
                    branchStatus = b.BranchStatus,
                    warehouseCount = warehouseCounts.GetValueOrDefault(b.BranchId, 0),
                    totalQuantity = hasAgg ? agg!.TotalQuantity : 0m,
                    reservedQuantity = hasAgg ? agg!.ReservedQuantity : 0m,
                    totalValue = hasAgg ? agg!.TotalValue : 0m,
                    lowStockCount = hasAgg ? agg!.LowStockCount : 0,
                    negativeStockCount = hasAgg ? agg!.NegativeStockCount : 0
                };
            }).ToList();
        }

        public async Task<object> GetWarehouseStructureAsync(int? page = null, int? pageSize = null)
        {
            var hasPaging = page.HasValue || pageSize.HasValue;
            var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);

            var (warehouseRows, total) = await _store.GetWarehouseStructurePageAsync(page, pageSize);
            var warehouses = warehouseRows
                .Select(w => new WarehouseStructureRow
                {
                    id = w.Id,
                    code = w.Code,
                    name = w.Name,
                    type = w.Type,
                    branchId = w.BranchId,
                    branchName = w.BranchName,
                    isActive = w.IsActive,
                    managerId = w.ManagerId
                })
                .ToList();

            var binCounts = await _store.GetWarehouseBinCountsAsync();
            var bins = binCounts
                .Select(b => new
                {
                    warehouseId = b.WarehouseId,
                    bin = b.Bin,
                    skuCount = b.SkuCount
                })
                .ToList();

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

        public async Task<object> GetTransactionsAsync(
            long? branchId,
            long? warehouseId,
            long? productId,
            string? type,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            string? search = null)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);

            var (rows, total) = await _store.GetAdminTransactionsPageAsync(
                branchId, warehouseId, productId, type, from, to, page, pageSize, search);

            if (rows.Count == 0)
            {
                return new { items = Array.Empty<object>(), total, page, pageSize };
            }

            var items = rows.Select(MapTransactionRow);
            return new { items, total, page, pageSize };
        }

        public async Task<object> GetReportsAsync(int? page = null, int? pageSize = null)
        {
            var inventoryRows = await _store.GetInventoryRowsForReportAsync();
            var transactions = await _store.GetTransactionsForReportAsync();
            var stockByBranch = BuildStockByBranch(inventoryRows);
            var topOutbound = BuildTopOutboundProducts(transactions);
            var nearOutOfStock = inventoryRows
                .Where(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel)
                .Take(50)
                .Cast<object>()
                .ToList();
            var defectiveStocks = inventoryRows
                .Where(IsDefectiveWarehouse)
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

            var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);

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

            var logs = await _store.GetInventoryAuditLogsAsync(inventoryTables, take);

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

        private static (int Page, int PageSize) NormalizePaging(int? page, int? pageSize)
        {
            var normalizedPage = page.GetValueOrDefault(1);
            if (normalizedPage < 1) normalizedPage = 1;

            var normalizedPageSize = pageSize.GetValueOrDefault(20);
            if (normalizedPageSize < 1) normalizedPageSize = 1;
            if (normalizedPageSize > 200) normalizedPageSize = 200;

            return (normalizedPage, normalizedPageSize);
        }

        private static decimal ComputeInventoryValue(Inventory inventory)
            => inventory.TotalInventoryValue ?? (inventory.Quantity * (inventory.CurrentMacPrice ?? inventory.Product.CostPrice ?? 0));

        private static bool IsDefectiveWarehouse(Inventory inventory)
            => inventory.Warehouse.Type != null && inventory.Warehouse.Type.ToUpper().Contains("DEFECTIVE");

        private static List<object> BuildStockByBranch(IEnumerable<Inventory> inventoryRows)
        {
            return inventoryRows
                .GroupBy(i => new { i.Warehouse.BranchId, i.Warehouse.Branch.Name })
                .Select(g => new StockByBranchViewModel(
                    g.Key.BranchId,
                    g.Key.Name,
                    g.Sum(x => x.Quantity),
                    g.Sum(ComputeInventoryValue)))
                .OrderBy(x => x.BranchName)
                .Cast<object>()
                .ToList();
        }

        private static List<object> BuildTopOutboundProducts(IEnumerable<StockTransaction> transactions)
        {
            return transactions
                .Where(t => IsOutboundType(t.Type))
                .GroupBy(t => new { t.ProductId, t.Product.Sku, t.Product.Name })
                .Select(g => new TopOutboundProductViewModel(
                    g.Key.ProductId,
                    g.Key.Sku,
                    g.Key.Name,
                    g.Sum(x => Math.Abs(x.Quantity))))
                .OrderByDescending(x => x.TotalOutboundQty)
                .Take(20)
                .Cast<object>()
                .ToList();
        }

        private static bool IsOutboundType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return false;
            var normalized = type.Trim().ToUpperInvariant();
            return normalized.Contains("OUT") || normalized.Contains("ISSUE") || normalized.Contains("XUAT");
        }

        private static object MapTransactionRow(AdminStockTransactionRowData row)
        {
            var source = ResolveSourceType(
                row.Transaction.PurchaseOrderId,
                row.Transaction.RetailOrderId,
                row.Transaction.RetailReturnId,
                row.Transaction.MaterialRequestId,
                row.Transaction.MaterialReturnId);

            return new
            {
                id = row.Transaction.Id,
                createdAt = row.Transaction.CreatedAt,
                date = row.Transaction.CreatedAt,
                type = row.Transaction.Type,
                quantity = row.Transaction.Quantity,
                qty = row.Transaction.Quantity,
                price = row.Transaction.Price,
                note = row.Transaction.Note,
                @ref = !string.IsNullOrWhiteSpace(row.Transaction.Note) ? row.Transaction.Note : source,
                branchId = row.Branch.Id,
                branchName = row.Branch.Name,
                warehouseId = row.Warehouse.Id,
                warehouseName = row.Warehouse.Name,
                productId = row.Transaction.ProductId,
                productSku = row.Transaction.Product.Sku,
                partCode = row.Transaction.Product.Sku,
                productName = row.Transaction.Product.Name,
                partName = row.Transaction.Product.Name,
                createdBy = row.Transaction.CreatedBy,
                createdByName = row.Transaction.CreatedByNavigation != null ? row.Transaction.CreatedByNavigation.FullName : null,
                by = row.Transaction.CreatedByNavigation != null ? row.Transaction.CreatedByNavigation.FullName : null,
                source
            };
        }

        private static string ResolveSourceType(
            long? purchaseOrderId,
            long? retailOrderId,
            long? retailReturnId,
            long? materialRequestId,
            long? materialReturnId)
        {
            if (purchaseOrderId.HasValue) return "PurchaseOrder";
            if (retailOrderId.HasValue) return "RetailOrder";
            if (retailReturnId.HasValue) return "RetailReturn";
            if (materialRequestId.HasValue) return "MaterialRequest";
            if (materialReturnId.HasValue) return "MaterialReturn";
            return "Unknown";
        }

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

        private sealed record StockByBranchViewModel(
            long BranchId,
            string BranchName,
            decimal TotalQuantity,
            decimal TotalValue
        );

        private sealed record TopOutboundProductViewModel(
            long ProductId,
            string Sku,
            string ProductName,
            decimal TotalOutboundQty
        );
    }
}
