namespace MyGarage.Application.GatePasses;

public sealed record GatePassListItem(long Id, long ServiceOrderId, string? ClearanceType, string? QrCode,
    long IssuedBy, long? ApprovedBy, string? ResolutionNotes, string? OldPartsPackingListUrl,
    bool? IsOldPartsChecked, string? Status, long? ScannedBy, DateTime? SecurityCheckedAt, DateTime? PassedAt);

public interface IGatePassListReader
{
    Task<(IReadOnlyList<GatePassListItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class GatePassListQuery(IGatePassListReader reader)
{
    public Task<(IReadOnlyList<GatePassListItem> Items, int Total)> ExecuteAsync(int page, int pageSize, CancellationToken cancellationToken)
        => reader.ReadAsync(page, pageSize, cancellationToken);
}
