using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class PaymentAuthorizationService : IPaymentAuthorizationService
{
    private readonly IGarageContext _context;

    public PaymentAuthorizationService(IGarageContext context)
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

    private static bool HasAccountingOrManagementRole(UserActorContext actor)
    {
        if (actor.IsAdmin) return true;
        var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        return roleCode.Contains("ADMIN")
            || roleCode.Contains("MANAGER")
            || roleCode.Contains("ACCOUNTANT")
            || roleCode.Contains("FINANCE")
            || roleCode.Contains("KE_TOAN")
            || roleCode.Contains("ADVISOR")
            || roleCode.Contains("RECEPTIONIST");
    }

    public Task<bool> CanViewPaymentsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (IsStaff(actor))
        {
            if (branchId.HasValue && branchId.Value > 0)
            {
                return Task.FromResult(!actor.BranchId.HasValue || actor.BranchId.Value == branchId.Value);
            }
            return Task.FromResult(true);
        }

        // Customers must use dedicated endpoint (/api/payments/mine)
        return Task.FromResult(false);
    }

    public async Task<bool> CanViewPaymentAsync(long paymentId, UserActorContext actor, CancellationToken ct = default)
    {
        if (paymentId <= 0 || !actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;

        var payment = await _context.Payments
            .AsNoTracking()
            .Where(p => p.Id == paymentId)
            .Select(p => new
            {
                p.Id,
                p.PayerId,
                p.PayerType,
                p.ServiceOrderId,
                p.RetailOrderId,
                p.InvoiceId
            })
            .FirstOrDefaultAsync(ct);

        if (payment == null) return false;

        if (IsCustomer(actor))
        {
            if (string.Equals(payment.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) &&
                payment.PayerId == actor.UserId.Value)
            {
                return true;
            }

            if (payment.ServiceOrderId.HasValue)
            {
                var isSoOwner = await _context.ServiceOrders
                    .AsNoTracking()
                    .AnyAsync(so => so.Id == payment.ServiceOrderId.Value && so.CustomerId == actor.UserId.Value, ct);
                if (isSoOwner) return true;
            }

            if (payment.RetailOrderId.HasValue)
            {
                var isRoOwner = await _context.RetailOrders
                    .AsNoTracking()
                    .AnyAsync(ro => ro.Id == payment.RetailOrderId.Value && ro.CustomerId == actor.UserId.Value, ct);
                if (isRoOwner) return true;
            }

            return false;
        }

        if (IsStaff(actor))
        {
            if (!actor.BranchId.HasValue) return true;

            // Resolve branch from service order or retail order
            if (payment.ServiceOrderId.HasValue)
            {
                var soBranch = await _context.ServiceOrders
                    .AsNoTracking()
                    .Where(so => so.Id == payment.ServiceOrderId.Value)
                    .Select(so => (long?)so.ExecuteBranchId)
                    .FirstOrDefaultAsync(ct);
                if (soBranch.HasValue && soBranch.Value != actor.BranchId.Value) return false;
            }
            else if (payment.RetailOrderId.HasValue)
            {
                var roBranch = await _context.RetailOrders
                    .AsNoTracking()
                    .Where(ro => ro.Id == payment.RetailOrderId.Value)
                    .Select(ro => (long?)ro.BranchId)
                    .FirstOrDefaultAsync(ct);
                if (roBranch.HasValue && roBranch.Value != actor.BranchId.Value) return false;
            }

            return true;
        }

        return false;
    }

    public Task<bool> CanCreatePaymentAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (IsStaff(actor))
        {
            if (branchId.HasValue && branchId.Value > 0 && actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
            {
                return Task.FromResult(false);
            }
            return Task.FromResult(HasAccountingOrManagementRole(actor));
        }

        return Task.FromResult(false);
    }

    public async Task<bool> CanTransitionPaymentStatusAsync(long paymentId, UserActorContext actor, CancellationToken ct = default)
    {
        if (paymentId <= 0 || !actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;

        if (!IsStaff(actor) || !HasAccountingOrManagementRole(actor)) return false;

        return await CanViewPaymentAsync(paymentId, actor, ct);
    }

    public async Task<bool> CanRefundPaymentAsync(long paymentId, UserActorContext actor, CancellationToken ct = default)
    {
        if (paymentId <= 0 || !actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;

        if (!IsStaff(actor)) return false;

        var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        var hasRefundPrivilege = roleCode.Contains("ADMIN")
            || roleCode.Contains("MANAGER")
            || roleCode.Contains("ACCOUNTANT")
            || roleCode.Contains("FINANCE")
            || roleCode.Contains("KE_TOAN");

        if (!hasRefundPrivilege) return false;

        return await CanViewPaymentAsync(paymentId, actor, ct);
    }
}
