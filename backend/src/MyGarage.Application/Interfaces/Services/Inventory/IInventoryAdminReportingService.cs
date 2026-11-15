using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IInventoryAdminReportingService
    {
        Task<object> GetOverviewAsync();
        Task<IEnumerable<object>> GetBranchSummaryAsync();
        Task<object> GetWarehouseStructureAsync(int? page = null, int? pageSize = null);
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
        Task<object> GetReportsAsync(int? page = null, int? pageSize = null);
        Task<IReadOnlyList<object>> GetInventoryAuditLogAsync(int take);
    }
}
