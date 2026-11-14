namespace MyGarage.Application.Approvals;

public sealed record ApprovalListItem(long Id, string? RequestType, long? ServiceOrderDiscountId,
    long? GatePassId, long? MaterialReturnId, long RequestedBy, long? ApproverId, string? Status,
    string? Reason, string? RejectReason, DateTime? CreatedAt, DateTime? UpdatedAt);

public interface IApprovalListReader
{
    Task<(IReadOnlyList<ApprovalListItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class ApprovalListQuery(IApprovalListReader reader)
{
    public Task<(IReadOnlyList<ApprovalListItem> Items, int Total)> ExecuteAsync(int page, int pageSize, CancellationToken cancellationToken)
        => reader.ReadAsync(page, pageSize, cancellationToken);
}
