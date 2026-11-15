namespace MyGarage.Application.Interfaces.Common;

public sealed record AuditActorSnapshot(
    long? UserId,
    string? Role,
    long? BranchId,
    string? IpAddress,
    string? UserAgent,
    string? TraceId);

public interface IAuditContextAccessor
{
    AuditActorSnapshot? GetCurrentContext();
}
