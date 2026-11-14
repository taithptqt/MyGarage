using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class PayrollAuthorizationService : IPayrollAuthorizationService
{
    private readonly IGarageContext _context;

    public PayrollAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    private static bool IsStaff(UserActorContext actor)
    {
        if (!actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;
        return string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasPayrollRole(UserActorContext actor)
    {
        if (actor.IsAdmin) return true;
        if (!IsStaff(actor)) return false;

        var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        return roleCode.Contains("ADMIN")
            || roleCode.Contains("MANAGER")
            || roleCode.Contains("ACCOUNTANT")
            || roleCode.Contains("FINANCE")
            || roleCode.Contains("KE_TOAN")
            || roleCode.Contains("HR_MANAGER")
            || roleCode.Contains("NHAN_SU");
    }

    public Task<bool> CanViewSalaryProfilesAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (!HasPayrollRole(actor)) return Task.FromResult(false);

        if (branchId.HasValue && branchId.Value > 0 && actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> CanManageSalaryProfilesAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (!IsStaff(actor)) return Task.FromResult(false);

        var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        var canManage = roleCode.Contains("ADMIN")
            || roleCode.Contains("HR_MANAGER")
            || roleCode.Contains("MANAGER");

        if (!canManage) return Task.FromResult(false);

        if (branchId.HasValue && branchId.Value > 0 && actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> CanViewPayrollRunsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (!HasPayrollRole(actor)) return Task.FromResult(false);

        if (branchId.HasValue && branchId.Value > 0 && actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> CanCalculatePayrollAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (!HasPayrollRole(actor)) return Task.FromResult(false);

        if (branchId.HasValue && branchId.Value > 0 && actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public async Task<bool> CanApprovePayrollAsync(long payrollRunId, UserActorContext actor, CancellationToken ct = default)
    {
        if (payrollRunId <= 0 || !actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;

        if (!HasPayrollRole(actor)) return false;

        if (actor.BranchId.HasValue)
        {
            var runBranchId = await _context.PayrollRuns
                .AsNoTracking()
                .Where(r => r.Id == payrollRunId)
                .Select(r => (long?)r.BranchId)
                .FirstOrDefaultAsync(ct);

            if (runBranchId.HasValue && runBranchId.Value != actor.BranchId.Value)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<bool> CanMarkPaidPayrollAsync(long payrollRunId, UserActorContext actor, CancellationToken ct = default)
    {
        if (payrollRunId <= 0 || !actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;

        if (!IsStaff(actor)) return false;

        var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        var canPay = roleCode.Contains("ADMIN")
            || roleCode.Contains("ACCOUNTANT")
            || roleCode.Contains("FINANCE")
            || roleCode.Contains("KE_TOAN")
            || roleCode.Contains("MANAGER");

        if (!canPay) return false;

        if (actor.BranchId.HasValue)
        {
            var runBranchId = await _context.PayrollRuns
                .AsNoTracking()
                .Where(r => r.Id == payrollRunId)
                .Select(r => (long?)r.BranchId)
                .FirstOrDefaultAsync(ct);

            if (runBranchId.HasValue && runBranchId.Value != actor.BranchId.Value)
            {
                return false;
            }
        }

        return true;
    }

    public Task<bool> CanViewMyPayslipAsync(long staffId, UserActorContext actor, CancellationToken ct = default)
    {
        if (staffId <= 0 || !actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        return Task.FromResult(actor.UserId.Value == staffId);
    }

    public async Task<bool> CanAccessStaffSalaryAsync(long staffId, UserActorContext actor, CancellationToken ct = default)
    {
        if (staffId <= 0 || !actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;

        if (await CanViewMyPayslipAsync(staffId, actor, ct)) return true;

        return HasPayrollRole(actor);
    }
}
