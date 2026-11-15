using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AuditLogService : GenericService<AuditLog>, IAuditLogService
    {
        private readonly IGarageContext _context;

        public AuditLogService(IAuditLogRepository repository, IGarageContext context) : base(repository)
        {
            _context = context;
        }

        public async Task<(IReadOnlyList<AuditLog> Items, int Total)> GetAdminChangesAsync(
            int page,
            int pageSize,
            string? action = null,
            string? tableName = null,
            string? query = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 1 : (pageSize > 200 ? 200 : pageSize);

            var logsQuery = _context.AuditLogs
                .AsNoTracking()
                .Where(x => x.TableName != null && x.TableName.ToUpper() != "AUDITLOGS");

            if (!string.IsNullOrWhiteSpace(action))
            {
                var normalized = action.Trim().ToUpperInvariant();
                logsQuery = logsQuery.Where(x => x.Action != null && x.Action.ToUpper() == normalized);
            }

            if (!string.IsNullOrWhiteSpace(tableName))
            {
                var normalized = tableName.Trim().ToUpperInvariant();
                logsQuery = logsQuery.Where(x => x.TableName != null && x.TableName.ToUpper().Contains(normalized));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalized = query.Trim().ToUpperInvariant();
                logsQuery = logsQuery.Where(x =>
                    (x.TableName != null && x.TableName.ToUpper().Contains(normalized)) ||
                    (x.Action != null && x.Action.ToUpper().Contains(normalized)) ||
                    (x.OldValue != null && x.OldValue.ToUpper().Contains(normalized)) ||
                    (x.NewValue != null && x.NewValue.ToUpper().Contains(normalized)));
            }

            if (from.HasValue)
            {
                logsQuery = logsQuery.Where(x => x.Timestamp.HasValue && x.Timestamp.Value >= from.Value);
            }

            if (to.HasValue)
            {
                logsQuery = logsQuery.Where(x => x.Timestamp.HasValue && x.Timestamp.Value <= to.Value);
            }

            var total = await logsQuery.CountAsync();
            var items = await logsQuery
                .OrderByDescending(x => x.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }
    }
}

