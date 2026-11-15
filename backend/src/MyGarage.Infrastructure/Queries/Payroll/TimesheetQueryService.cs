using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfTimesheetQueryService : ITimesheetQueryService
{
    private readonly IGarageContext _context;

    public EfTimesheetQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<TimesheetEntryOwnerInfo?> GetEntryOwnerAsync(long timesheetEntryId, CancellationToken ct = default)
    {
        return await _context.TimesheetEntries
            .AsNoTracking()
            .Where(t => t.Id == timesheetEntryId)
            .Select(t => new TimesheetEntryOwnerInfo(t.StaffId, t.BranchId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<long?> GetStaffBranchIdAsync(long staffId, CancellationToken ct = default)
    {
        return await _context.Staffs
            .AsNoTracking()
            .Where(s => s.Id == staffId && s.IsDeleted != true)
            .Select(s => (long?)s.BranchId)
            .FirstOrDefaultAsync(ct);
    }
}
