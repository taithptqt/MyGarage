using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Approvals;
using MyGarage.Interfaces;

namespace MyGarage.Tests;

// Compatibility adapter for tests that seed the legacy InMemory context.
public sealed class ApprovalListFixtureReader(IGarageContext db) : IApprovalListReader
{
    public async Task<(IReadOnlyList<ApprovalListItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var source = db.ApprovalRequests.AsNoTracking();
        var total = await source.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total) return (Array.Empty<ApprovalListItem>(), total);
        var items = await source.OrderBy(x => x.Id).Skip((int)offset).Take(pageSize)
            .Select(x => new ApprovalListItem(x.Id, x.RequestType, x.ServiceOrderDiscountId, x.GatePassId,
                x.MaterialReturnId, x.RequestedBy, x.ApproverId, x.Status, x.Reason, x.RejectReason,
                x.CreatedAt, x.UpdatedAt)).ToListAsync(cancellationToken);
        return (items, total);
    }
}
