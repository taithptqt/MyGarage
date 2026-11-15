using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
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

        List<AuditLogResponseDto> MapToDtos(IEnumerable<AuditLog> items);
        Task<AuditLogResponseDto?> GetDtoByIdAsync(long id);
        Task<AuditLogResponseDto> CreateDtoAsync(AuditLogCreateDto itemDto);
        Task<AuditLogResponseDto?> UpdateDtoAsync(long id, AuditLogUpdateDto itemDto);
    }
}
