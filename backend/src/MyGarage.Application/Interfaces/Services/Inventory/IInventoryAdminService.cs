using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IInventoryAdminService : IStockAlertSyncService
    {
        Task<object> GetOverviewAsync();
        Task<IEnumerable<object>> GetBranchSummaryAsync();
        Task<object> GetWarehouseStructureAsync(int? page = null, int? pageSize = null);
        Task<IReadOnlyList<object>> GetAlertsAsync();
        Task<IReadOnlyList<object>> GetStockAlertsAsync(bool? resolved = false, int take = 200);
        Task<object> GetTransactionsAsync(
            long? branchId,
            long? warehouseId,
            long? productId,
            string? type,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            string? search = null);
        Task<IReadOnlyList<object>> GetApprovalsAsync(string? status);
        Task<bool> ApproveRequestAsync(long id, long? approverId, string? note);
        Task<bool> RejectRequestAsync(long id, long? approverId, string? note);
        Task<IReadOnlyList<object>> GetAuditsAsync(string? status);
        Task<bool> ApproveAuditAsync(long id, long? approverId);
        Task<bool> RejectAuditAsync(long id, long? approverId);
        Task<object> GetReportsAsync(int? page = null, int? pageSize = null);
        Task<IReadOnlyList<object>> GetInventoryAuditLogAsync(int take);
    }
}

