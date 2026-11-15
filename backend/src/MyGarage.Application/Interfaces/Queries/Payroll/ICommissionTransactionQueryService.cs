using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface ICommissionTransactionQueryService
{
    Task<IReadOnlyList<CommissionTransaction>> GetCommissionsAsync(
        long? scopedBranchId,
        long? staffId,
        string? status,
        DateTime? monthStart,
        DateTime? monthEndExclusive,
        CancellationToken ct = default);

    Task<(IReadOnlyList<CommissionTransaction> Items, int Total)> GetPageAsync(
        long? scopedBranchId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<CommissionTransaction?> GetByIdWithStaffAsync(long id, CancellationToken ct = default);
    Task<long?> GetStaffBranchIdAsync(long staffId, CancellationToken ct = default);
}
