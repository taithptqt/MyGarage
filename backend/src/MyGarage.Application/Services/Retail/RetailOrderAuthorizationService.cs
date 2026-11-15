using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class RetailOrderAuthorizationService : IRetailOrderAuthorizationService
{
    private readonly IGarageContext _context;

    public RetailOrderAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    private static bool IsStaff(UserActorContext actor)
    {
        if (!actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;
        return string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCustomer(UserActorContext actor)
    {
        if (!actor.UserId.HasValue) return false;
        return string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase);
    }

    public Task<bool> CanViewRetailOrdersAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (IsStaff(actor))
        {
            if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0) return Task.FromResult(false);
            if (branchId.HasValue && branchId.Value > 0 && branchId.Value != actor.BranchId.Value) return Task.FromResult(false);
            return Task.FromResult(true);
        }

        if (IsCustomer(actor))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public async Task<bool> CanViewRetailOrderAsync(long retailOrderId, UserActorContext actor, CancellationToken ct = default)
    {
        if (retailOrderId <= 0 || !actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;

        var order = await _context.RetailOrders
            .AsNoTracking()
            .Where(o => o.Id == retailOrderId)
            .Select(o => new { o.BranchId, o.CustomerId })
            .FirstOrDefaultAsync(ct);

        if (order == null) return false;

        if (IsStaff(actor))
        {
            return !actor.BranchId.HasValue || actor.BranchId.Value == order.BranchId;
        }

        if (IsCustomer(actor))
        {
            return actor.UserId.Value == order.CustomerId;
        }

        return false;
    }

    public Task<bool> CanCheckoutRetailOrderAsync(long branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue || branchId <= 0) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (IsStaff(actor))
        {
            return Task.FromResult(!actor.BranchId.HasValue || actor.BranchId.Value == branchId);
        }

        if (IsCustomer(actor))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public async Task<bool> CanCancelRetailOrderAsync(long retailOrderId, UserActorContext actor, CancellationToken ct = default)
    {
        if (retailOrderId <= 0 || !actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;

        var order = await _context.RetailOrders
            .AsNoTracking()
            .Where(o => o.Id == retailOrderId)
            .Select(o => new { o.BranchId, o.CustomerId, o.Status })
            .FirstOrDefaultAsync(ct);

        if (order == null) return false;

        if (!RetailOrderLifecycleRules.CanCancel(order.Status))
        {
            return false;
        }

        if (IsStaff(actor))
        {
            return !actor.BranchId.HasValue || actor.BranchId.Value == order.BranchId;
        }

        if (IsCustomer(actor))
        {
            return actor.UserId.Value == order.CustomerId;
        }

        return false;
    }

    public async Task<bool> CanDeleteRetailOrderAsync(long retailOrderId, UserActorContext actor, CancellationToken ct = default)
    {
        if (retailOrderId <= 0 || !IsStaff(actor)) return false;
        if (actor.IsAdmin) return true;

        var orderBranchId = await _context.RetailOrders
            .AsNoTracking()
            .Where(o => o.Id == retailOrderId)
            .Select(o => (long?)o.BranchId)
            .FirstOrDefaultAsync(ct);

        if (!orderBranchId.HasValue) return false;
        return !actor.BranchId.HasValue || actor.BranchId.Value == orderBranchId.Value;
    }

    public Task<bool> CanViewRetailReturnsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!IsStaff(actor)) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0) return Task.FromResult(false);
        if (branchId.HasValue && branchId.Value > 0 && branchId.Value != actor.BranchId.Value) return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public async Task<bool> CanFinalizeRetailReturnAsync(long retailReturnId, UserActorContext actor, CancellationToken ct = default)
    {
        if (retailReturnId <= 0 || !IsStaff(actor)) return false;
        if (actor.IsAdmin) return true;

        var returnBranchId = await _context.RetailReturns
            .AsNoTracking()
            .Where(r => r.Id == retailReturnId)
            .Select(r => (long?)r.RetailOrder.BranchId)
            .FirstOrDefaultAsync(ct);

        if (!returnBranchId.HasValue) return false;
        return !actor.BranchId.HasValue || actor.BranchId.Value == returnBranchId.Value;
    }
}
