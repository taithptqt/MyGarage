using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Payments;

namespace MyGarage.Services;

public sealed class PaymentAuthorizationService : IPaymentAuthorizationService
{
    private readonly IPaymentStore _store;

    public PaymentAuthorizationService(IPaymentStore store)
    {
        _store = store;
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

        var payment = await _store.GetPaymentAuthInfoAsync(paymentId, ct);
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
                if (await _store.IsServiceOrderOwnedByCustomerAsync(payment.ServiceOrderId.Value, actor.UserId.Value, ct))
                {
                    return true;
                }
            }

            if (payment.RetailOrderId.HasValue)
            {
                if (await _store.IsRetailOrderOwnedByCustomerAsync(payment.RetailOrderId.Value, actor.UserId.Value, ct))
                {
                    return true;
                }
            }

            return false;
        }

        if (IsStaff(actor))
        {
            if (!actor.BranchId.HasValue) return true;

            // Resolve branch from service order or retail order
            if (payment.ServiceOrderId.HasValue)
            {
                var soBranch = await _store.GetBranchIdForServiceOrderAsync(payment.ServiceOrderId.Value, ct);
                if (soBranch.HasValue && soBranch.Value != actor.BranchId.Value) return false;
            }
            else if (payment.RetailOrderId.HasValue)
            {
                var roBranch = await _store.GetBranchIdForRetailOrderAsync(payment.RetailOrderId.Value, ct);
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
