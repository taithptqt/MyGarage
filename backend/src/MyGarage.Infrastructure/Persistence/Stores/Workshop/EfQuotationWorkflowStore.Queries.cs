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
    public Task<Quotation?> GetQuotationWithDetailsAndDiscountsAsync(long quotationId)
    {
        return _context.Quotations
            .Include(x => x.QuotationDetails)
            .Include(x => x.QuotationDiscounts)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
    }

    public Task<bool> QuotationExistsAsync(long quotationId)
    {
        return _context.Quotations.AsNoTracking().AnyAsync(x => x.Id == quotationId);
    }

    public Task<List<QuotationDiscount>> GetQuotationDiscountsAsNoTrackingAsync(long quotationId)
    {
        return _context.QuotationDiscounts.AsNoTracking()
            .Where(x => x.QuotationId == quotationId && x.QuotationDetailId == null)
            .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
    }

    public void RemoveQuotationDiscounts(IEnumerable<QuotationDiscount> rows)
    {
        _context.QuotationDiscounts.RemoveRange(rows);
    }

    public void AddQuotationDiscounts(IEnumerable<QuotationDiscount> rows)
    {
        _context.QuotationDiscounts.AddRange(rows);
    }

    public void AddQuotationDiscount(QuotationDiscount row)
    {
        _context.QuotationDiscounts.Add(row);
    }

    public Task<List<Quotation>> GetQuotationsByServiceOrderWithDetailsAndDiscountsAsync(long serviceOrderId)
    {
        return _context.Quotations
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderByDescending(x => x.VersionNo)
            .ThenByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .Include(x => x.QuotationDetails)
            .Include(x => x.QuotationDiscounts)
            .ToListAsync();
    }

    public Task<List<MandatoryOverrideRequest>> GetMandatoryOverrideRequestsByServiceOrderAsync(long serviceOrderId)
    {
        return _context.MandatoryOverrideRequests.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderByDescending(x => x.RequestedAt)
            .Include(x => x.Quotation)
            .Include(x => x.QuotationDetail)
            .ToListAsync();
    }

    public Task<BranchQuotationApprovalPolicy?> GetBranchQuotationApprovalPolicyAsNoTrackingAsync(long branchId)
    {
        return _context.BranchQuotationApprovalPolicies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId);
    }

    public Task<QuotationBranchInfo?> GetBranchInfoAsNoTrackingAsync(long branchId)
    {
        return _context.Branches.AsNoTracking()
            .Where(x => x.Id == branchId)
            .Select(x => new QuotationBranchInfo(x.Id, x.ManagerId))
            .FirstOrDefaultAsync();
    }

    public Task<BranchQuotationApprovalPolicy?> GetBranchQuotationApprovalPolicyForUpdateAsync(long branchId)
    {
        return _context.BranchQuotationApprovalPolicies
            .FirstOrDefaultAsync(x => x.BranchId == branchId);
    }

    public void AddBranchQuotationApprovalPolicy(BranchQuotationApprovalPolicy entity)
    {
        _context.BranchQuotationApprovalPolicies.Add(entity);
    }

    public Task<string?> GetStaffEmailAsync(long staffId)
    {
        return _context.Staffs.AsNoTracking()
            .Where(x => x.Id == staffId)
            .Select(x => x.Email)
            .FirstOrDefaultAsync();
    }

    public Task<List<QuotationBranchStaffCandidate>> GetBranchStaffCandidatesAsync(long branchId)
    {
        return (
            from staff in _context.Staffs.AsNoTracking()
            join role in _context.Roles.AsNoTracking() on staff.RoleId equals role.Id
            where staff.BranchId == branchId
            select new QuotationBranchStaffCandidate(
                staff.Id,
                staff.Email,
                role.RoleCode,
                role.RoleName)
        ).ToListAsync();
    }

    public Task<List<QuotationBaselineRecommendationInfo>> GetBaselineRecommendationsAsync(IReadOnlyList<long> recommendationIds)
    {
        return _context.DiagnosisRecommendations.AsNoTracking()
            .Where(x => recommendationIds.Contains(x.Id))
            .Select(x => new QuotationBaselineRecommendationInfo(
                x.Id,
                x.ItemType,
                x.ItemType == "PART" ? x.InventoryItemId : x.StandardServiceId,
                x.Quantity,
                x.Urgency))
            .ToListAsync();
    }

    public Task<long?> GetServiceOrderExecuteBranchIdAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.AsNoTracking()
            .Where(x => x.Id == serviceOrderId)
            .Select(x => (long?)x.ExecuteBranchId)
            .FirstOrDefaultAsync();
    }

    public Task<BranchQuotationApprovalPolicy?> GetActiveBranchQuotationApprovalPolicyAsNoTrackingAsync(long branchId)
    {
        return _context.BranchQuotationApprovalPolicies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.IsActive);
    }

    public Task<decimal?> GetPreviousVersionInsuranceAmountAsync(long serviceOrderId, int previousVersionNo)
    {
        return _context.Quotations.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId && x.VersionNo == previousVersionNo)
            .Select(x => (decimal?)x.TotalInsuranceAmount)
            .FirstOrDefaultAsync();
    }

    public Task<QuotationInitialDiagnosisMeta?> GetInitialQuotationDiagnosisMetaAsync(long serviceOrderId)
    {
        return _context.Quotations.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderBy(x => x.VersionNo)
            .ThenBy(x => x.CreatedAt ?? DateTime.MinValue)
            .ThenBy(x => x.Id)
            .Select(x => new QuotationInitialDiagnosisMeta(x.Id, x.DiagnosisAssignmentId))
            .FirstOrDefaultAsync();
    }

    public Task<QuotationServiceOrderDetailTotals?> GetNonCanceledServiceOrderDetailTotalsAsync(long serviceOrderId, string canceledStatus)
    {
        return _context.ServiceOrderDetails.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Where(x => x.Status == null || x.Status.ToUpper() != canceledStatus.ToUpper())
            .GroupBy(_ => 1)
            .Select(group => new QuotationServiceOrderDetailTotals(
                group.Sum(x => x.SubTotal),
                group.Sum(x => x.DiscountAllocatedAmount ?? 0m),
                group.Sum(x => x.TaxAmount),
                group.Sum(x => x.FinalAmount)))
            .FirstOrDefaultAsync();
    }

    public Task<List<QuotationSodSupplementaryRow>> GetSodSupplementaryRowsAsync(long serviceOrderId)
    {
        return _context.ServiceOrderDetails.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Where(x => x.QuotationDetailId.HasValue && x.QuotationDetailId.Value > 0)
            .Select(x => new QuotationSodSupplementaryRow(
                x.QuotationDetailId!.Value,
                x.IsSupplementary,
                x.ServiceSource,
                x.SupplementaryReasonCode,
                x.SourceQuotationId,
                x.UpdatedAt,
                x.Id))
            .ToListAsync();
    }

    public Task<List<QuotationLineageRow>> GetQuotationLineageRowsAsync(long serviceOrderId)
    {
        return _context.Quotations.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Select(x => new QuotationLineageRow(x.Id, x.VersionNo, x.DiagnosisAssignmentId))
            .ToListAsync();
    }

    public Task<List<QuotationDetailLineageRow>> GetQuotationDetailLineageRowsAsync(long serviceOrderId)
    {
        return _context.QuotationDetails.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Select(x => new QuotationDetailLineageRow(x.Id, x.QuotationId, x.SourceQuotationDetailId))
            .ToListAsync();
    }

    public Task<long?> GetActiveVoucherIdByCodeAsync(string voucherCode)
    {
        return _context.Vouchers.AsNoTracking()
            .Where(x => x.Code == voucherCode && (x.IsDeleted == null || x.IsDeleted == false))
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync();
    }

    public Task<QuotationCancellationOrderInfo?> GetCancellationOrderInfoAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.AsNoTracking()
            .Where(x => x.Id == serviceOrderId)
            .Select(x => new QuotationCancellationOrderInfo(x.Id, x.Code, x.ReceptionistId, x.ServiceAdvisorId))
            .FirstOrDefaultAsync();
    }
}
