using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Vouchers;

public sealed partial class EfVoucherStore
{
    public Task<VoucherApprovalRequest?> GetApprovalRequestByIdAsync(long id, bool track = false, CancellationToken ct = default)
    {
        var query = track ? _context.VoucherApprovalRequests : _context.VoucherApprovalRequests.AsNoTracking();
        return query.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task SaveApprovalRequestChangesAsync(VoucherApprovalRequest request, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
    }
}
