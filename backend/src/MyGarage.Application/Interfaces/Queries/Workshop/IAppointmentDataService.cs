using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IAppointmentDataService
{
    Task<(bool Found, long CustomerId, long BranchId)> GetScopeMetaAsync(long id, CancellationToken ct = default);
    Task<List<AuditLog>> GetEditRequestLogsAsync(long appointmentId, CancellationToken ct = default);
    Task DecideEditRequestInTransactionAsync(
        AuditLog requestLog,
        string? beforeDecision,
        long actorUserId,
        DateTime now,
        CancellationToken ct = default);
    Task AddInsuranceUploadAsync(AppointmentInsuranceUpload upload, CancellationToken ct = default);
    Task<(string RoleCode, string RoleName)> GetStaffRoleInfoAsync(long staffId, CancellationToken ct = default);
}
