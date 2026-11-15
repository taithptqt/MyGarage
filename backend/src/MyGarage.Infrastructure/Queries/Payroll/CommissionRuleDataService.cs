using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfCommissionRuleDataService(IGarageContext db) : ICommissionRuleDataService
{
    private const string TargetTypeServiceAdvisor = "SERVICE_ADVISOR";

    public async Task<IReadOnlyList<ServiceAdvisorCommissionRuleRow>> GetServiceAdvisorRulesAsync(
        long? branchId,
        long? staffTierId,
        bool? isActive,
        CancellationToken ct = default)
    {
        var query = db.CommissionRules
            .AsNoTracking()
            .Where(x => x.IsDeleted != true && x.TargetType == TargetTypeServiceAdvisor);

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (staffTierId.HasValue)
        {
            query = query.Where(x => x.StaffTierId == staffTierId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => (x.IsActive ?? true) == isActive.Value);
        }

        return await query
            .OrderBy(x => x.BranchId)
            .ThenBy(x => x.StaffTierId)
            .ThenByDescending(x => x.IsActive)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new ServiceAdvisorCommissionRuleRow(
                x.Id,
                x.BranchId,
                x.StaffTierId,
                x.TargetId,
                x.CommissionType,
                x.CommissionValue,
                x.IsActive ?? true,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<(CommissionRule Rule, bool Created)> UpsertServiceAdvisorRuleAsync(
        long branchId,
        long staffTierId,
        long? targetId,
        string commissionType,
        decimal commissionValue,
        bool isActive,
        CancellationToken ct = default)
    {
        var existing = await db.CommissionRules
            .FirstOrDefaultAsync(x =>
                x.IsDeleted != true &&
                x.BranchId == branchId &&
                x.StaffTierId == staffTierId &&
                x.TargetType == TargetTypeServiceAdvisor &&
                x.TargetId == targetId, ct);

        var now = VietnamTime.Now;
        if (existing == null)
        {
            var entity = new CommissionRule
            {
                BranchId = branchId,
                StaffTierId = staffTierId,
                TargetType = TargetTypeServiceAdvisor,
                TargetId = targetId,
                CommissionType = commissionType,
                CommissionValue = commissionValue,
                IsActive = isActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.CommissionRules.Add(entity);
            await db.SaveChangesAsync(ct);
            return (entity, true);
        }

        existing.CommissionType = commissionType;
        existing.CommissionValue = commissionValue;
        existing.IsActive = isActive;
        existing.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return (existing, false);
    }

    public Task<bool> BranchExistsAsync(long branchId, CancellationToken ct = default)
        => db.Branches.AsNoTracking().AnyAsync(x => x.Id == branchId && x.IsDeleted != true, ct);

    public Task<bool> StaffTierExistsAsync(long staffTierId, CancellationToken ct = default)
        => db.StaffTiers.AsNoTracking().AnyAsync(x => x.Id == staffTierId && x.IsDeleted != true, ct);
}
