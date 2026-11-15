using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed class EfWorkshopAuthorizationStore : IWorkshopAuthorizationStore
{
    private readonly IGarageContext _context;

    public EfWorkshopAuthorizationStore(IGarageContext context)
    {
        _context = context;
    }

    public Task<QuotationAuthMeta?> GetQuotationAuthMetaAsync(long quotationId, CancellationToken ct = default)
    {
        return _context.Quotations
            .AsNoTracking()
            .Where(q => q.Id == quotationId)
            .Select(q => new QuotationAuthMeta(
                q.ServiceOrder != null ? (long?)q.ServiceOrder.CustomerId : null,
                q.ServiceOrder != null ? (long?)q.ServiceOrder.ExecuteBranchId : null))
            .FirstOrDefaultAsync(ct);
    }

    public Task<ServiceOrderAuthMeta?> GetServiceOrderAuthMetaAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return _context.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => new ServiceOrderAuthMeta(so.CustomerId, so.ExecuteBranchId, so.OriginBranchId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> IsTechnicianAssignedToDetailAsync(long serviceOrderDetailId, long technicianId, CancellationToken ct = default)
    {
        var isAssigned = await _context.ServiceStaffAssignments
            .AsNoTracking()
            .AnyAsync(a => a.ServiceOrderDetailId == serviceOrderDetailId && a.TechnicianId == technicianId, ct);

        if (!isAssigned)
        {
            isAssigned = await _context.ServiceOrderDetails
                .AsNoTracking()
                .AnyAsync(d => d.Id == serviceOrderDetailId && d.TechnicianId == technicianId, ct);
        }

        return isAssigned;
    }

    public async Task<bool> DidTechnicianWorkOnOrderAsync(long serviceOrderId, long technicianId, CancellationToken ct = default)
    {
        var workedOnOrder = await _context.ServiceStaffAssignments
            .AsNoTracking()
            .AnyAsync(a => a.ServiceOrderDetail.ServiceOrderId == serviceOrderId && a.TechnicianId == technicianId, ct);

        if (!workedOnOrder)
        {
            workedOnOrder = await _context.ServiceOrderDetails
                .AsNoTracking()
                .AnyAsync(d => d.ServiceOrderId == serviceOrderId && d.TechnicianId == technicianId, ct);
        }

        return workedOnOrder;
    }

    public async Task<(bool Found, long? BranchId)> GetEditRequestBranchIdAsync(long requestId, CancellationToken ct = default)
    {
        var requestLog = await _context.AuditLogs
            .AsNoTracking()
            .Where(log => log.Id == requestId &&
                          log.Action == "REQUEST_EDIT" &&
                          (log.TableName == "SERVICE_ORDERS" || log.TableName == "SERVICEORDERS"))
            .Select(log => new { log.BranchId })
            .FirstOrDefaultAsync(ct);

        return requestLog == null ? (false, null) : (true, requestLog.BranchId);
    }
}
