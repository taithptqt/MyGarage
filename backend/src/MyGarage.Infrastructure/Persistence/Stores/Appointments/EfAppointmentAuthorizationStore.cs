using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.Appointments;

public sealed class EfAppointmentAuthorizationStore : IAppointmentAuthorizationStore
{
    private readonly IGarageContext _context;

    public EfAppointmentAuthorizationStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task<AppointmentOwnerBranchInfo?> GetAppointmentOwnerAndBranchAsync(long appointmentId, CancellationToken ct = default)
    {
        var meta = await _context.Appointments
            .AsNoTracking()
            .Where(a => a.Id == appointmentId)
            .Select(a => new { a.CustomerId, a.BranchId })
            .FirstOrDefaultAsync(ct);

        return meta == null ? null : new AppointmentOwnerBranchInfo(meta.CustomerId, meta.BranchId);
    }

    public async Task<EditRequestBranchInfo?> GetEditRequestBranchAsync(long requestId, CancellationToken ct = default)
    {
        var requestLog = await _context.AuditLogs
            .AsNoTracking()
            .Where(log => log.Id == requestId &&
                          log.Action == "REQUEST_EDIT" &&
                          log.TableName == "APPOINTMENTS")
            .Select(log => new { log.BranchId })
            .FirstOrDefaultAsync(ct);

        return requestLog == null ? null : new EditRequestBranchInfo(requestLog.BranchId);
    }
}
