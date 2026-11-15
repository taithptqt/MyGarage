using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Application.Inventory;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InventoryAdminService : IInventoryAdminService
    {
        private readonly IInventoryAdminReportingService _reportingService;
        private readonly IInventoryAdminWorkflowService _workflowService;

        public InventoryAdminService(
            IInventoryAdminReportingService reportingService,
            IInventoryAdminWorkflowService workflowService)
        {
            _reportingService = reportingService;
            _workflowService = workflowService;
        }

        internal InventoryAdminService(IInventoryStore store)
            : this(new InventoryAdminReportingService(store), new InventoryAdminWorkflowService(store))
        {
        }

        public Task<object> GetOverviewAsync()
            => _reportingService.GetOverviewAsync();

        public Task<IEnumerable<object>> GetBranchSummaryAsync()
            => _reportingService.GetBranchSummaryAsync();

        public Task<object> GetWarehouseStructureAsync(int? page = null, int? pageSize = null)
            => _reportingService.GetWarehouseStructureAsync(page, pageSize);

        public Task<IReadOnlyList<object>> GetAlertsAsync()
            => _workflowService.GetAlertsAsync();

        public Task<IReadOnlyList<object>> GetStockAlertsAsync(bool? resolved = false, int take = 200)
            => _workflowService.GetStockAlertsAsync(resolved, take);

        public Task<IReadOnlyList<object>> RebuildStockAlertsAsync(long? actorUserId = null)
            => _workflowService.RebuildStockAlertsAsync(actorUserId);

        public Task<bool> ResolveStockAlertAsync(long alertId, long purchaseOrderId, long? actorUserId = null)
            => _workflowService.ResolveStockAlertAsync(alertId, purchaseOrderId, actorUserId);

        public Task<object> GetTransactionsAsync(
            long? branchId,
            long? warehouseId,
            long? productId,
            string? type,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            string? search = null)
            => _reportingService.GetTransactionsAsync(branchId, warehouseId, productId, type, from, to, page, pageSize, search);

        public Task<IReadOnlyList<object>> GetApprovalsAsync(string? status)
            => _workflowService.GetApprovalsAsync(status);

        public Task<bool> ApproveRequestAsync(long id, long? approverId, string? note)
            => _workflowService.ApproveRequestAsync(id, approverId, note);

        public Task<bool> RejectRequestAsync(long id, long? approverId, string? note)
            => _workflowService.RejectRequestAsync(id, approverId, note);

        public Task<IReadOnlyList<object>> GetAuditsAsync(string? status)
            => _workflowService.GetAuditsAsync(status);

        public Task<bool> ApproveAuditAsync(long id, long? approverId)
            => _workflowService.ApproveAuditAsync(id, approverId);

        public Task<bool> RejectAuditAsync(long id, long? approverId)
            => _workflowService.RejectAuditAsync(id, approverId);

        public Task<object> GetReportsAsync(int? page = null, int? pageSize = null)
            => _reportingService.GetReportsAsync(page, pageSize);

        public Task<IReadOnlyList<object>> GetInventoryAuditLogAsync(int take)
            => _reportingService.GetInventoryAuditLogAsync(take);
    }
}
