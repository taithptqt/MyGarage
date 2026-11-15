namespace MyGarage.Application.Interfaces.Queries;

public sealed record ShiftReconciliationResult(
    bool Found,
    bool AlreadyClosed,
    long ShiftId = 0,
    string? Status = null,
    decimal OpeningBalance = 0m,
    decimal? TotalCashIn = null,
    decimal TransferIn = 0m,
    decimal OnlineIn = 0m,
    decimal? TotalCashOut = null,
    decimal? ExpectedBalance = null,
    decimal? ActualBalance = null,
    decimal? Discrepancy = null,
    DateTime? ClosedAt = null);

public interface IShiftClosingDataService
{
    Task<ShiftReconciliationResult> ReconcileShiftAsync(
        long id,
        decimal? actualBalance,
        decimal? totalCashOut,
        long? managerId,
        string? note,
        long? actorId,
        CancellationToken ct = default);
}
