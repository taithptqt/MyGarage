using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record ServiceAdvisorCommissionRuleRow(
    long Id,
    long? BranchId,
    long? StaffTierId,
    long? TargetId,
    string CommissionType,
    decimal CommissionValue,
    bool IsActive,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public interface ICommissionRuleDataService
{
    Task<IReadOnlyList<ServiceAdvisorCommissionRuleRow>> GetServiceAdvisorRulesAsync(
        long? branchId,
        long? staffTierId,
        bool? isActive,
        CancellationToken ct = default);

    Task<(CommissionRule Rule, bool Created)> UpsertServiceAdvisorRuleAsync(
        long branchId,
        long staffTierId,
        long? targetId,
        string commissionType,
        decimal commissionValue,
        bool isActive,
        CancellationToken ct = default);

    Task<bool> BranchExistsAsync(long branchId, CancellationToken ct = default);
    Task<bool> StaffTierExistsAsync(long staffTierId, CancellationToken ct = default);
}
