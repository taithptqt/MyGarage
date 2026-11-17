using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Interfaces;
using MyGarage.Application.Inventory;

namespace MyGarage.Services;

public sealed class InventoryAdminWorkflowService : IInventoryAdminWorkflowService
{
    private readonly IInventoryAdminStockAlertService _alerts;
    private readonly IInventoryAdminApprovalService _approvals;

    public InventoryAdminWorkflowService(IInventoryAdminStockAlertService alerts, IInventoryAdminApprovalService approvals)
    {
        _alerts = alerts;
        _approvals = approvals;
    }

    public InventoryAdminWorkflowService(IInventoryStore store)
        : this(new InventoryAdminStockAlertService(store), new InventoryAdminApprovalService(store)) { }

    public Task<IReadOnlyList<object>> GetAlertsAsync() => _alerts.GetAlertsAsync();
    public Task<IReadOnlyList<object>> GetStockAlertsAsync(bool? resolved = false, int take = 200) => _alerts.GetStockAlertsAsync(resolved, take);
    public Task<IReadOnlyList<object>> RebuildStockAlertsAsync(long? actorUserId = null) => _alerts.RebuildStockAlertsAsync(actorUserId);
    public Task<bool> ResolveStockAlertAsync(long alertId, long purchaseOrderId, long? actorUserId = null) => _alerts.ResolveStockAlertAsync(alertId, purchaseOrderId, actorUserId);
    public Task<IReadOnlyList<object>> GetApprovalsAsync(string? status) => _approvals.GetApprovalsAsync(status);
    public Task<bool> ApproveRequestAsync(long id, long? approverId, string? note) => _approvals.ApproveRequestAsync(id, approverId, note);
    public Task<bool> RejectRequestAsync(long id, long? approverId, string? note) => _approvals.RejectRequestAsync(id, approverId, note);
    public Task<IReadOnlyList<object>> GetAuditsAsync(string? status) => _approvals.GetAuditsAsync(status);
    public Task<bool> ApproveAuditAsync(long id, long? approverId) => _approvals.ApproveAuditAsync(id, approverId);
    public Task<bool> RejectAuditAsync(long id, long? approverId) => _approvals.RejectAuditAsync(id, approverId);
}