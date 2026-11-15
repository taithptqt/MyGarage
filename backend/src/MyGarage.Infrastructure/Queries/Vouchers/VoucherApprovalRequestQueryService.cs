using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfVoucherApprovalRequestQueryService : IVoucherApprovalRequestQueryService
{
    private readonly IGarageContext _context;

    public EfVoucherApprovalRequestQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<VoucherApprovalRequest> Items, int Total)> GetPagedAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.VoucherApprovalRequests
            .AsNoTracking()
            .Include(x => x.Voucher)
            .Include(x => x.Redemption)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            query = query.Where(x => x.Status != null && x.Status.ToUpper() == normalized);
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? new List<VoucherApprovalRequest>()
            : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<VoucherApprovalRequest> Items, int Total)> GetAdminPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.VoucherApprovalRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(x => ((x.RequestType ?? "null") + " " + (x.Reason ?? "null") + " " + x.Id.ToString() + " " +
                (x.VoucherId.HasValue ? x.VoucherId.Value.ToString() : "null")).ToUpper().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? new List<VoucherApprovalRequest>()
            : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (items, total);
    }
}
