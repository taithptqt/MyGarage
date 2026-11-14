using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[ApiController]
[Route("api/appointments/reception-list")]
[Route("api/v1/appointments/reception-list")]
[Authorize(Roles = "Staff")]
public class ReceptionAppointmentListController(IGarageContext context, IAppointmentDetailAssembler assembler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long? branchId, [FromQuery] string? search,
        [FromQuery] string? status, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] string? sort, [FromQuery] string? mode, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied)) return (IActionResult)denied!;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var scoped = context.Appointments.AsNoTracking().AsQueryable();
        if (scopedBranchId.HasValue) scoped = scoped.Where(a => a.BranchId == scopedBranchId.Value);
        var statuses = await scoped.Select(a => a.Status).Distinct().OrderBy(s => s).ToListAsync();
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
        var singleCount = await query.CountAsync(a => a.GroupSessionId == null);
        var grouped = query.Where(a => a.GroupSessionId != null).GroupBy(a => a.GroupSessionId!.Value)
            .Select(g => new { Id = g.Key, Created = g.Max(a => a.CreatedAt ?? a.UpdatedAt ?? DateTime.MinValue) });
        var groupCount = await grouped.CountAsync();
        var offset = ((long)page - 1) * pageSize;
        var ids = new List<long>();
        if (string.Equals(mode, "GROUP", StringComparison.OrdinalIgnoreCase))
        {
            if (offset < groupCount)
            {
                var groupIds = await (sort == "OLDEST" ? grouped.OrderBy(g => g.Created).ThenBy(g => g.Id)
                    : grouped.OrderByDescending(g => g.Created).ThenByDescending(g => g.Id))
                    .Skip((int)offset).Take(pageSize).Select(g => g.Id).ToListAsync();
                ids = await query.Where(a => a.GroupSessionId != null && groupIds.Contains(a.GroupSessionId.Value))
                    .Select(a => a.Id).ToListAsync();
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
            ids = await ordered.Skip((int)offset).Take(pageSize).Select(a => a.Id).ToListAsync();
        }
        var entities = await context.Appointments.AsNoTracking().Where(a => ids.Contains(a.Id))
            .Include(a => a.Branch).Include(a => a.Customer).Include(a => a.GroupSession)
            .Include(a => a.Vehicle).ThenInclude(v => v!.Model).ThenInclude(m => m.Brand).ToListAsync();
        var byId = entities.ToDictionary(a => a.Id);
        var items = await assembler.AssembleManyAsync(ids.Where(byId.ContainsKey).Select(id => byId[id]));
        return Ok(new { items, singleCount, groupCount, statuses });
    }
}
