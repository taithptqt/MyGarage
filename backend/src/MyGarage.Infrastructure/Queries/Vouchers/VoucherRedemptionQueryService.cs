using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfVoucherRedemptionQueryService(IGarageContext context) : IVoucherRedemptionQueryService
{
    public async Task<(IReadOnlyList<VoucherRedemption> Items, int Total)> GetPageAsync(
        long? branchId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.VoucherRedemptions.AsNoTracking().Include(x => x.Voucher).AsQueryable();
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            query = query.Where(x => x.RedemptionStatus != null && x.RedemptionStatus.ToUpper() == normalized);
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var paged = offset >= total
            ? []
            : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return (paged, total);
    }

    public async Task<VoucherRedemptionAdminPageResult> GetAdminPageAsync(
        long? branchId,
        string? search,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.VoucherRedemptions.AsNoTracking().AsQueryable();
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(x => ((x.VoucherCode ?? "null") + " " + x.Id.ToString() + " " +
                (x.CustomerId.HasValue ? x.CustomerId.Value.ToString() : "null")).ToUpper().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var totalDiscount = await query.SumAsync(x => (decimal?)x.DiscountAmount, ct) ?? 0m;
        var offset = ((long)page - 1) * pageSize;
        var ordered = string.Equals(sort, "discountDesc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(x => x.DiscountAmount).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            : query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
        var rows = offset >= total
            ? []
            : await ordered.Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return new VoucherRedemptionAdminPageResult(rows, total, totalDiscount);
    }

    public Task<VoucherRedemption?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => context.VoucherRedemptions.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

    public Task<VoucherRedemption?> FindRedemptionByIdAsync(long id, CancellationToken ct = default)
        => context.VoucherRedemptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<VoucherApprovalRequest?> FindApprovalRequestByIdAsync(long approvalRequestId, CancellationToken ct = default)
        => context.VoucherApprovalRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == approvalRequestId, ct);

    public Task<VoucherRedemptionServiceOrderScope?> GetServiceOrderScopeAsync(long serviceOrderId, CancellationToken ct = default)
        => context.ServiceOrders.AsNoTracking().Where(x => x.Id == serviceOrderId)
            .Select(x => new VoucherRedemptionServiceOrderScope(x.ExecuteBranchId, x.CustomerId, x.VehicleId))
            .FirstOrDefaultAsync(ct);

    public Task<VoucherRedemptionRetailOrderScope?> GetRetailOrderScopeAsync(long retailOrderId, CancellationToken ct = default)
        => context.RetailOrders.AsNoTracking().Where(x => x.Id == retailOrderId)
            .Select(x => new VoucherRedemptionRetailOrderScope(x.BranchId, x.CustomerId))
            .FirstOrDefaultAsync(ct);

    public Task<VoucherRedemptionInvoiceScope?> GetInvoiceScopeAsync(long invoiceId, CancellationToken ct = default)
        => context.Invoices.AsNoTracking().Where(x => x.Id == invoiceId)
            .Select(x => new VoucherRedemptionInvoiceScope(
                x.ServiceOrderId,
                x.RetailOrderId,
                x.VehicleId,
                x.ServiceOrder != null ? (long?)x.ServiceOrder.ExecuteBranchId : null,
                x.RetailOrder != null ? (long?)x.RetailOrder.BranchId : null,
                x.ServiceOrder != null ? (long?)x.ServiceOrder.CustomerId : null,
                x.RetailOrder != null ? x.RetailOrder.CustomerId : null,
                x.ServiceOrder != null ? (long?)x.ServiceOrder.VehicleId : null))
            .FirstOrDefaultAsync(ct);
}
