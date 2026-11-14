using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Audit;
using MyGarage.Common;

namespace MyGarage.Infrastructure.Audit;

public sealed class AdminAuditReader(AuditReadDbContext context) : IAdminAuditReader
{
    public async Task<AdminAuditPage> ReadAsync(AdminAuditFilter filter, CancellationToken cancellationToken)
    {
        var query = context.Logs.AsNoTracking().Where(x => x.TableName != null && x.TableName.ToUpper() != "AUDITLOGS");
        if (filter.Action != null) query = query.Where(x => x.Action != null && x.Action.ToUpper() == filter.Action);
        if (filter.TableName != null) query = query.Where(x => x.TableName != null && x.TableName.ToUpper().Contains(filter.TableName));
        if (filter.Query != null) query = query.Where(x =>
            (x.TableName != null && x.TableName.ToUpper().Contains(filter.Query)) ||
            (x.Action != null && x.Action.ToUpper().Contains(filter.Query)) ||
            (x.OldValue != null && x.OldValue.ToUpper().Contains(filter.Query)) ||
            (x.NewValue != null && x.NewValue.ToUpper().Contains(filter.Query)));
        if (filter.From.HasValue) query = query.Where(x => x.Timestamp.HasValue && x.Timestamp.Value >= filter.From.Value);
        if (filter.To.HasValue) query = query.Where(x => x.Timestamp.HasValue && x.Timestamp.Value <= filter.To.Value);
        var total = await query.CountAsync(cancellationToken);
        var offset = ((long)filter.Page - 1) * filter.PageSize;
        if (offset >= total) return new AdminAuditPage(Array.Empty<AdminAuditRow>(), total, filter.Page, filter.PageSize);
        var logs = await query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id)
            .Skip((int)offset).Take(filter.PageSize).ToListAsync(cancellationToken);
        var rows = logs.Select(x => new AdminAuditRow(x.Id, x.UserId, Utf8MojibakeRepair.Fix(x.UserType), x.BranchId,
            Utf8MojibakeRepair.Fix(x.Action), Utf8MojibakeRepair.Fix(x.TableName), x.RecordId,
            Utf8MojibakeRepair.Fix(x.OldValue), Utf8MojibakeRepair.Fix(x.NewValue), Utf8MojibakeRepair.Fix(x.IpAddress),
            Utf8MojibakeRepair.Fix(x.UserAgent), Utf8MojibakeRepair.Fix(x.SessionId), x.Timestamp)).ToArray();
        return new AdminAuditPage(rows, total, filter.Page, filter.PageSize);
    }
}
