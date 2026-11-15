using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IBayAdminWriteDataService
{
    Task<bool> BranchExistsAsync(long branchId, CancellationToken ct = default);

    Task<bool> BayCodeExistsAsync(string code, long? excludeBayId = null, CancellationToken ct = default);

    Task<bool> BaySequenceOrderExistsAsync(long branchId, int sequenceOrder, long? excludeBayId = null, CancellationToken ct = default);

    Task<int> GetNextSequenceOrderAsync(long branchId, long? excludeBayId = null, CancellationToken ct = default);

    Task<Bay?> GetBayByIdAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<Bay>> GetBaysByIdsAsync(IReadOnlyCollection<long> bayIds, CancellationToken ct = default);

    Task AddBayAsync(Bay bay, CancellationToken ct = default);

    Task SaveChangesInTransactionAsync(CancellationToken ct = default);
}
