using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Customers;
using MyGarage.Domain.Rules;

namespace MyGarage.Services;

public sealed class CustomerAuthorizationService : ICustomerAuthorizationService
{
    private readonly ICustomerStore _store;

    public CustomerAuthorizationService(ICustomerStore store)
    {
        _store = store;
    }

    public async Task<bool> CanAccessCustomerAsync(long customerId, UserActorContext actor, CancellationToken ct = default)
    {
        if (customerId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0)
            {
                return true; // Staff without branch restriction has global access
            }

            var branchId = actor.BranchId.Value;
            var (exists, note) = await _store.GetActiveCustomerNoteAsync(customerId, ct);
            if (!exists)
            {
                return false;
            }

            // Check preferred branch tag
            var prefBranch = CustomerDomainRules.ExtractPreferredBranchId(note);
            if (prefBranch.HasValue && prefBranch.Value == branchId)
            {
                return true;
            }

            // Check service orders
            if (await _store.HasBranchServiceOrderAsync(customerId, branchId, ct))
            {
                return true;
            }

            // Check appointments
            if (await _store.HasBranchAppointmentAsync(customerId, branchId, ct))
            {
                return true;
            }

            return false;
        }

        if (string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return actor.UserId.Value == customerId;
        }

        return false;
    }

    public Task<bool> CanManageCustomersAsync(UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        if (actor.IsAdmin || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> CanAccessBranchCustomersAsync(long? requestedBranchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        if (actor.IsAdmin)
        {
            return Task.FromResult(true);
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            if (!actor.BranchId.HasValue || !requestedBranchId.HasValue)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(actor.BranchId.Value == requestedBranchId.Value);
        }

        return Task.FromResult(false);
    }
}
