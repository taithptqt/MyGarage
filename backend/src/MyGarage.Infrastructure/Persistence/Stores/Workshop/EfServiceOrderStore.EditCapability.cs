using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Workshop;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed partial class EfServiceOrderStore
{
    public Task<List<AssignmentLockRow>> GetAssignmentLockRowsAsync(long serviceOrderId)
    {
        return (
            from assignment in _context.ServiceStaffAssignments.AsNoTracking()
            join detail in _context.ServiceOrderDetails.AsNoTracking()
                on assignment.ServiceOrderDetailId equals detail.Id
            where detail.ServiceOrderId == serviceOrderId
            select new AssignmentLockRow(
                assignment.ActualStartTime,
                assignment.ActualEndTime,
                assignment.Status))
            .ToListAsync();
    }

    public Task<bool> HasIssuedDetailsAsync(long serviceOrderId)
    {
        return _context.ServiceOrderDetails
            .AsNoTracking()
            .AnyAsync(detail =>
                detail.ServiceOrderId == serviceOrderId &&
                detail.IssuedQuantity.HasValue &&
                detail.IssuedQuantity.Value > 0m);
    }

    public Task<List<MaterialRequestLockRow>> GetMaterialRequestLockRowsAsync(long serviceOrderId)
    {
        return (
            from requestDetail in _context.MaterialRequestDetails.AsNoTracking()
            join request in _context.MaterialRequests.AsNoTracking()
                on requestDetail.RequestId equals request.Id
            where request.ServiceOrderId == serviceOrderId
            select new MaterialRequestLockRow(
                requestDetail.IssuedQuantity,
                requestDetail.Status,
                request.Status))
            .ToListAsync();
    }

    public Task<bool> HasIssuedOrPaidInvoiceAsync(long serviceOrderId, string unpaidStatus)
    {
        return _context.Invoices
            .AsNoTracking()
            .AnyAsync(invoice =>
                invoice.ServiceOrderId == serviceOrderId &&
                (invoice.IssuedAt.HasValue ||
                 (invoice.PaidAmount ?? 0m) > 0m ||
                 (invoice.PaymentStatus != null &&
                  invoice.PaymentStatus != string.Empty &&
                  invoice.PaymentStatus.ToUpper() != unpaidStatus)));
    }

    public Task<List<string>> GetPaymentStatusesAsync(long serviceOrderId)
    {
        return _context.Payments
            .AsNoTracking()
            .Where(payment => payment.ServiceOrderId == serviceOrderId && payment.Status != null)
            .Select(payment => payment.Status!)
            .ToListAsync();
    }

    public Task<List<GatePassLockRow>> GetGatePassLockRowsAsync(long serviceOrderId)
    {
        return _context.GatePasses
            .AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Select(x => new GatePassLockRow(x.Status, x.PassedAt))
            .ToListAsync();
    }

    public Task<List<EditRequestAuditLogRow>> GetEditRequestAuditLogValuesAsync(long serviceOrderId, string action, string tableName, int take)
    {
        return _context.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.Action == action &&
                log.TableName == tableName &&
                log.RecordId == serviceOrderId)
            .OrderByDescending(log => log.Id)
            .Take(take)
            .Select(log => new EditRequestAuditLogRow(log.Id, log.NewValue))
            .ToListAsync();
    }
}
