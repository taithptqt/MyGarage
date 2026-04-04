using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IInventoryAdminService
    {
        Task<object> GetOverviewAsync();
        Task<IEnumerable<object>> GetBranchSummaryAsync();
        Task<object> GetWarehouseStructureAsync();
        Task<IReadOnlyList<object>> GetAlertsAsync();
        Task<object> GetTransactionsAsync(
            long? branchId,
            long? warehouseId,
            long? productId,
            string? type,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize);
        Task<IReadOnlyList<object>> GetApprovalsAsync(string? status);
        Task<bool> ApproveRequestAsync(long id, long? approverId, string? note);
        Task<bool> RejectRequestAsync(long id, long? approverId, string? note);
        Task<IReadOnlyList<object>> GetAuditsAsync(string? status);
        Task<bool> ApproveAuditAsync(long id, long? approverId);
        Task<bool> RejectAuditAsync(long id, long? approverId);
        Task<object> GetReportsAsync();
        Task<IReadOnlyList<object>> GetInventoryAuditLogAsync(int take);
    }
}

