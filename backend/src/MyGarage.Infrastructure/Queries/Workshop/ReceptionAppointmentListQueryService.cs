using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfReceptionAppointmentListQueryService(IGarageContext context) : IReceptionAppointmentListQueryService
{
    public async Task<ReceptionAppointmentListResult> GetAsync(
        long? scopedBranchId,
        string? search,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? sort,
        string? mode,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var scoped = context.Appointments.AsNoTracking().AsQueryable();
        if (scopedBranchId.HasValue) scoped = scoped.Where(a => a.BranchId == scopedBranchId.Value);
        var statuses = await scoped.Select(a => a.Status).Distinct().OrderBy(s => s).ToListAsync(ct);
        var query = scoped;
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var token = status.Trim().ToUpperInvariant();
            query = query.Where(a => a.Status.ToUpper() == token);
        }
        if (dateFrom.HasValue) query = query.Where(a => a.StartTime >= dateFrom.Value.Date);
        if (dateTo.HasValue && dateTo.Value.Date < DateTime.MaxValue.Date)
        {
            var end = dateTo.Value.Date.AddDays(1);
            query = query.Where(a => a.StartTime < end);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            var compact = term.Replace(" ", "").Replace(".", "").Replace("-", "");
            query = query.Where(a => a.Customer.FullName.ToLower().Contains(term)
                || (a.Customer.Phone != null && a.Customer.Phone.ToLower().Contains(term))
                || (a.GroupSession != null && a.GroupSession.GroupCode.ToLower().Contains(term))
                || (a.Vehicle != null && (a.Vehicle.LicensePlate.ToLower().Contains(term)
                    || (compact != "" && a.Vehicle.LicensePlate.ToLower().Replace(" ", "").Replace(".", "").Replace("-", "").Contains(compact)))));
        }
        var singleCount = await query.CountAsync(a => a.GroupSessionId == null, ct);
        var grouped = query.Where(a => a.GroupSessionId != null).GroupBy(a => a.GroupSessionId!.Value)
            .Select(g => new { Id = g.Key, Created = g.Max(a => a.CreatedAt ?? a.UpdatedAt ?? DateTime.MinValue) });
        var groupCount = await grouped.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var ids = new List<long>();
        if (string.Equals(mode, "GROUP", StringComparison.OrdinalIgnoreCase))
        {
            if (offset < groupCount)
            {
                var groupIds = await (sort == "OLDEST" ? grouped.OrderBy(g => g.Created).ThenBy(g => g.Id)
                    : grouped.OrderByDescending(g => g.Created).ThenByDescending(g => g.Id))
                    .Skip((int)offset).Take(pageSize).Select(g => g.Id).ToListAsync(ct);
                ids = await query.Where(a => a.GroupSessionId != null && groupIds.Contains(a.GroupSessionId.Value))
                    .Select(a => a.Id).ToListAsync(ct);
            }
        }
        else if (offset < singleCount)
        {
            var singles = query.Where(a => a.GroupSessionId == null);
            var ordered = sort switch
            {
                "OLDEST" => singles.OrderBy(a => a.CreatedAt ?? a.UpdatedAt ?? DateTime.MinValue).ThenBy(a => a.Id),
                "NAME_ASC" => singles.OrderBy(a => a.Customer.FullName).ThenBy(a => a.Id),
                "NAME_DESC" => singles.OrderByDescending(a => a.Customer.FullName).ThenByDescending(a => a.Id),
                _ => singles.OrderByDescending(a => a.CreatedAt ?? a.UpdatedAt ?? DateTime.MinValue).ThenByDescending(a => a.Id)
            };
            ids = await ordered.Skip((int)offset).Take(pageSize).Select(a => a.Id).ToListAsync(ct);
        }
        var entities = await context.Appointments.AsNoTracking().Where(a => ids.Contains(a.Id))
            .Include(a => a.Branch).Include(a => a.Customer).Include(a => a.GroupSession)
            .Include(a => a.Vehicle).ThenInclude(v => v!.Model).ThenInclude(m => m.Brand).ToListAsync(ct);
        var byId = entities.ToDictionary(a => a.Id);
        var orderedEntities = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        return new ReceptionAppointmentListResult(orderedEntities, singleCount, groupCount, statuses);
    }
}
