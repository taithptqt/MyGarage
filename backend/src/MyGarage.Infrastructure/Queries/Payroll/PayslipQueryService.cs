using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfPayslipQueryService : IPayslipQueryService
{
    private readonly IGarageContext _context;

    public EfPayslipQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Payslip>> GetMyPayslipsAsync(long staffId, string? period = null, CancellationToken ct = default)
    {
        var query = _context.Payslips
            .AsNoTracking()
            .Include(p => p.PayrollItem)
                .ThenInclude(pi => pi.PayrollRun)
                    .ThenInclude(pr => pr.PayrollPeriod)
            .Include(p => p.PayrollItem)
                .ThenInclude(pi => pi.Staff)
            .Where(p => p.PayrollItem.StaffId == staffId);

        if (!string.IsNullOrWhiteSpace(period))
        {
            var normalizedPeriod = period.Trim().ToUpperInvariant();
            query = query.Where(p => p.PayrollItem.PayrollRun.PayrollPeriod.PeriodCode.ToUpper() == normalizedPeriod);
        }

        return await query
            .OrderByDescending(p => p.IssuedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync(ct);
    }
}
