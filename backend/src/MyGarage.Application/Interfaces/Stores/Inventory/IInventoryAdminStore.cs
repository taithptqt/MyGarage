using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Inventory;

public interface IInventoryAdminStore
{
    Task<IReadOnlyList<StockTransaction>> GetStockTransactionsWithoutSourceAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalRequest>> GetApprovalRequestsAsync(string? normalizedStatus, CancellationToken ct = default);
    Task<ApprovalRequest?> GetApprovalRequestByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryAudit>> GetInventoryAuditsAsync(string? normalizedStatus, CancellationToken ct = default);
    Task<InventoryAudit?> GetInventoryAuditByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<BranchSummaryRowData>> GetActiveBranchesForSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<long, int>> GetActiveWarehouseCountsByBranchAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<long, BranchInventoryAggregateData>> GetBranchInventoryAggregatesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MyGarage.Models.Inventory>> GetInventoryRowsForReportAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StockTransaction>> GetTransactionsForReportAsync(CancellationToken ct = default);
    Task<int> CountPendingApprovalsAsync(string status, CancellationToken ct = default);
    Task<int> CountPendingAuditsAsync(string status1, string status2, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetInventoryAuditLogsAsync(IReadOnlyCollection<string> tableNames, int take, CancellationToken ct = default);
    Task<(IReadOnlyList<WarehouseStructureRowData> Warehouses, int Total)> GetWarehouseStructurePageAsync(int? page, int? pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<WarehouseBinCountData>> GetWarehouseBinCountsAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<AdminStockTransactionRowData> Items, int Total)> GetAdminTransactionsPageAsync(
        long? branchId,
        long? warehouseId,
        long? productId,
        string? type,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default);
}
