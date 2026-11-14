namespace MyGarage.Application.Audit;

public sealed record AdminAuditFilter(int Page, int PageSize, string? Action, string? TableName,
    string? Query, DateTime? From, DateTime? To);
public sealed record AdminAuditRow(long Id, long UserId, string? UserType, long? BranchId, string? Action,
    string? TableName, long RecordId, string? OldValue, string? NewValue, string? IpAddress,
    string? UserAgent, string? SessionId, DateTime? Timestamp);
public sealed record AdminAuditPage(IReadOnlyList<AdminAuditRow> Items, int Total, int Page, int PageSize);

public interface IAdminAuditReader
{
    Task<AdminAuditPage> ReadAsync(AdminAuditFilter filter, CancellationToken cancellationToken);
}

public sealed class AdminAuditQuery(IAdminAuditReader reader)
{
    public Task<AdminAuditPage> ExecuteAsync(int page, int pageSize, string? action, string? tableName,
        string? query, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        return reader.ReadAsync(new AdminAuditFilter(Math.Max(1, page), Math.Clamp(pageSize, 1, 200),
            Normalize(action), Normalize(tableName), Normalize(query), from, to), cancellationToken);
    }
}
