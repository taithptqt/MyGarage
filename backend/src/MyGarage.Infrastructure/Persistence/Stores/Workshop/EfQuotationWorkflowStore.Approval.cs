using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed partial class EfQuotationWorkflowStore
{
    public void AddQuotationApprovalAction(QuotationApprovalAction action)
    {
        _context.QuotationApprovalActions.Add(action);
    }

    public Task<ServiceOrder?> GetServiceOrderWithVehicleForUpdateAsync(long serviceOrderId)
    {
        return _context.ServiceOrders
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == serviceOrderId);
    }

    public Task<ServiceOrder?> GetServiceOrderForUpdateAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
    }

    public Task<bool> HasPendingMandatoryOverrideAsync(long quotationId, string approvedStatus)
    {
        return _context.MandatoryOverrideRequests.AsNoTracking()
            .AnyAsync(x => x.QuotationId == quotationId && x.Status != approvedStatus);
    }

    public void AddAuditLog(AuditLog log)
    {
        _context.AuditLogs.Add(log);
    }

    public Task<List<ServiceOrderDetail>> GetServiceOrderDetailsForUpdateAsync(long serviceOrderId)
    {
        return _context.ServiceOrderDetails
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .ToListAsync();
    }

    public void AddServiceOrderDetails(IEnumerable<ServiceOrderDetail> rows)
    {
        _context.ServiceOrderDetails.AddRange(rows);
    }

    public Task<List<ServiceOrderDiscount>> GetServiceOrderDiscountsForUpdateAsync(long serviceOrderId)
    {
        return _context.ServiceOrderDiscounts
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .ToListAsync();
    }

    public void RemoveServiceOrderDiscounts(IEnumerable<ServiceOrderDiscount> rows)
    {
        _context.ServiceOrderDiscounts.RemoveRange(rows);
    }

    public void AddServiceOrderDiscounts(IEnumerable<ServiceOrderDiscount> rows)
    {
        _context.ServiceOrderDiscounts.AddRange(rows);
    }

    public Task<QuotationBranchManagerInfo?> GetServiceOrderBranchManagerInfoAsync(long quotationServiceOrderId)
    {
        return _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.Id == quotationServiceOrderId)
            .Select(x => new QuotationBranchManagerInfo(x.ExecuteBranchId, x.ExecuteBranch.ManagerId))
            .FirstOrDefaultAsync();
    }

    public void AddMandatoryOverrideRequests(IEnumerable<MandatoryOverrideRequest> rows)
    {
        _context.MandatoryOverrideRequests.AddRange(rows);
    }

    public Task<MandatoryOverrideRequest?> GetMandatoryOverrideRequestForUpdateAsync(long requestId)
    {
        return _context.MandatoryOverrideRequests.FirstOrDefaultAsync(x => x.Id == requestId);
    }

    public Task<Quotation?> GetQuotationFullForPortalAsync(long quotationId)
    {
        return _context.Quotations
            .Include(x => x.QuotationDetails)
            .Include(x => x.QuotationDiscounts)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Customer)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
    }

    public Task<Quotation?> GetQuotationWithCustomerForOtpAsync(long quotationId)
    {
        return _context.Quotations
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
    }

    public Task<Quotation?> GetQuotationAsNoTrackingAsync(long quotationId)
    {
        return _context.Quotations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == quotationId);
    }

    public Task<QuotationAccessLink?> GetLatestActiveAccessLinkForUpdateAsync(long quotationId, string activeStatus, DateTime now)
    {
        return _context.QuotationAccessLinks
            .Where(x => x.QuotationId == quotationId && x.Status == activeStatus && x.ExpiresAt > now)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
    }

    public Task<List<QuotationAccessLink>> GetActiveAccessLinksForUpdateAsync(long quotationId, string activeStatus)
    {
        return _context.QuotationAccessLinks
            .Where(x => x.QuotationId == quotationId && x.Status == activeStatus)
            .ToListAsync();
    }

    public Task<List<QuotationAccessLink>> GetSiblingActiveAccessLinksForUpdateAsync(long quotationId, string activeStatus, long excludeLinkId)
    {
        return _context.QuotationAccessLinks
            .Where(x => x.QuotationId == quotationId && x.Status == activeStatus && x.Id != excludeLinkId)
            .ToListAsync();
    }

    public void AddQuotationAccessLink(QuotationAccessLink link)
    {
        _context.QuotationAccessLinks.Add(link);
    }

    public Task<QuotationAccessLink?> GetAccessLinkByTokenHashForUpdateAsync(string tokenHash)
    {
        return _context.QuotationAccessLinks.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
    }
}
