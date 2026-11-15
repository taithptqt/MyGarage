using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderEditCapabilityQueryStore
{
    Task<ServiceOrder?> GetServiceOrderAsNoTrackingAsync(long id);
    Task<List<AssignmentLockRow>> GetAssignmentLockRowsAsync(long serviceOrderId);
    Task<bool> HasIssuedDetailsAsync(long serviceOrderId);
    Task<List<MaterialRequestLockRow>> GetMaterialRequestLockRowsAsync(long serviceOrderId);
    Task<bool> HasIssuedOrPaidInvoiceAsync(long serviceOrderId, string unpaidStatus);
    Task<List<string>> GetPaymentStatusesAsync(long serviceOrderId);
    Task<List<GatePassLockRow>> GetGatePassLockRowsAsync(long serviceOrderId);
    Task<List<EditRequestAuditLogRow>> GetEditRequestAuditLogValuesAsync(long serviceOrderId, string action, string tableName, int take);
}
