using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces;

// Internal workflows can append and update their audit-backed request records.
public interface IAuditLogWriter
{
    Task<AuditLog> AddAsync(AuditLog entity);
    Task<AuditLog?> GetByIdAsync(object id);
    Task<AuditLog?> UpdateAsync(object id, AuditLog entity);
}