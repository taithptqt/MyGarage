using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class AccountingAuthorizationService : IAccountingAuthorizationService
{
    private readonly IGarageContext _context;

    public AccountingAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    private static bool IsStaff(UserActorContext actor)
    {
        if (!actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;
        return string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAccountingRole(UserActorContext actor)
    {
        if (actor.IsAdmin) return true;
        if (!IsStaff(actor)) return false;

        var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        return roleCode.Contains("ADMIN")
            || roleCode.Contains("MANAGER")
            || roleCode.Contains("ACCOUNTANT")
            || roleCode.Contains("FINANCE")
            || roleCode.Contains("KE_TOAN");
    }

    public Task<bool> CanAccessTableAsync(string tableKey, long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (!IsStaff(actor)) return Task.FromResult(false);

        if (!HasAccountingRole(actor)) return Task.FromResult(false);

        if (branchId.HasValue && branchId.Value > 0 && actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> CanPostJournalEntryAsync(long journalEntryId, UserActorContext actor, CancellationToken ct = default)
    {
        if (journalEntryId <= 0 || !actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        return Task.FromResult(HasAccountingRole(actor));
    }

    public Task<bool> CanDecideFinanceApprovalAsync(long approvalId, UserActorContext actor, CancellationToken ct = default)
    {
        if (approvalId <= 0 || !actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (!IsStaff(actor)) return Task.FromResult(false);

        var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        var canDecide = roleCode.Contains("ADMIN")
            || roleCode.Contains("DIRECTOR")
            || roleCode.Contains("MANAGER")
            || roleCode.Contains("FINANCE");

        return Task.FromResult(canDecide);
    }

    public Task<bool> CanAccessFinancialReportsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (!HasAccountingRole(actor)) return Task.FromResult(false);

        if (branchId.HasValue && branchId.Value > 0 && actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
