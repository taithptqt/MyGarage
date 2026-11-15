using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IQuotationApprovalOrderSyncStore
{
    void AddQuotationApprovalAction(QuotationApprovalAction action);
    Task<ServiceOrder?> GetServiceOrderWithVehicleForUpdateAsync(long serviceOrderId);
    Task<ServiceOrder?> GetServiceOrderForUpdateAsync(long serviceOrderId);
    void AddAuditLog(AuditLog log);
    Task<List<ServiceOrderDetail>> GetServiceOrderDetailsForUpdateAsync(long serviceOrderId);
    void AddServiceOrderDetails(IEnumerable<ServiceOrderDetail> rows);
    Task<List<ServiceOrderDiscount>> GetServiceOrderDiscountsForUpdateAsync(long serviceOrderId);
    void RemoveServiceOrderDiscounts(IEnumerable<ServiceOrderDiscount> rows);
    void AddServiceOrderDiscounts(IEnumerable<ServiceOrderDiscount> rows);
    Task<QuotationBranchManagerInfo?> GetServiceOrderBranchManagerInfoAsync(long quotationServiceOrderId);
}
