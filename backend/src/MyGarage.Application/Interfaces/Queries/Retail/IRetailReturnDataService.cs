using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public enum RetailReturnFinalizeStatus
{
    Ok,
    AlreadyCompleted,
    NotFound,
    BadRequest
}

public sealed record RetailReturnFinalizeResult(
    RetailReturnFinalizeStatus Outcome,
    string Message,
    long? RetailReturnId = null,
    string? Status = null,
    decimal? TotalRefundAmount = null);

public interface IRetailReturnDataService
{
    Task<(IReadOnlyList<RetailReturn> Items, int Total)> GetPagedAsync(
        long? branchId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<RetailReturn?> GetByIdAsync(long id, long? branchId, CancellationToken ct = default);

    Task<bool> RetailOrderExistsAsync(long retailOrderId, long? branchId, CancellationToken ct = default);

    Task<RetailReturn?> GetForUpdateAsync(long id, long? branchId, CancellationToken ct = default);

    Task<RetailReturnFinalizeResult> FinalizeReturnAsync(
        long id,
        long actorUserId,
        long? scopedBranch,
        CancellationToken ct = default);
}
