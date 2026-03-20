using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AuditLogService : GenericService<AuditLog>, IAuditLogService
    {
        public AuditLogService(IAuditLogRepository repository) : base(repository)
        {
        }
    }
}
