using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAuditLogService : IGenericService<AuditLog>
    {
        Task<(IReadOnlyList<AuditLog> Items, int Total)> GetAdminChangesAsync(
            int page,
            int pageSize,
            string? action = null,
            string? tableName = null,
            string? query = null,
            DateTime? from = null,
            DateTime? to = null);
    }
}
