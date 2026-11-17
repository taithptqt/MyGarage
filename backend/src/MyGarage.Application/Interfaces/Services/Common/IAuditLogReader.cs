using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces;

public interface IAuditLogReader
{
    Task<(IReadOnlyList<AuditLog> Items, int Total)> GetPageByIdAsync(int page, int pageSize);
    List<AuditLogResponseDto> MapToDtos(IEnumerable<AuditLog> items);
    Task<AuditLogResponseDto?> GetDtoByIdAsync(long id);
}