using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public interface IInventoryAdminStockAlertService
{
    Task<IReadOnlyList<object>> GetAlertsAsync();
    Task<IReadOnlyList<object>> GetStockAlertsAsync(bool? resolved = false, int take = 200);
    Task<IReadOnlyList<object>> RebuildStockAlertsAsync(long? actorUserId = null);
    Task<bool> ResolveStockAlertAsync(long alertId, long purchaseOrderId, long? actorUserId = null);
}

public interface IInventoryAdminApprovalService
{
    Task<IReadOnlyList<object>> GetApprovalsAsync(string? status);
    Task<bool> ApproveRequestAsync(long id, long? approverId, string? note);
    Task<bool> RejectRequestAsync(long id, long? approverId, string? note);
    Task<IReadOnlyList<object>> GetAuditsAsync(string? status);
    Task<bool> ApproveAuditAsync(long id, long? approverId);
    Task<bool> RejectAuditAsync(long id, long? approverId);
}

public interface IInventoryAdminWorkflowService : IInventoryAdminStockAlertService, IInventoryAdminApprovalService { }