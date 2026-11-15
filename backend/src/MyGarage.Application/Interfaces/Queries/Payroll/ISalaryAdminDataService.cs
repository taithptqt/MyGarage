using MyGarage.Enums;

namespace MyGarage.Application.Interfaces.Queries;

public interface ISalaryAdminDataService
{
    Task<(bool Found, long? BranchId)> GetActiveStaffAsync(long staffId, CancellationToken ct = default);

    Task ApproveInitialSalaryRequestInTransactionAsync(
        long requestId,
        long staffId,
        long? profileBranchId,
        decimal baseSalary,
        DateOnly effectiveFrom,
        string? note,
        long? approverId,
        CancellationToken ct = default);
}
