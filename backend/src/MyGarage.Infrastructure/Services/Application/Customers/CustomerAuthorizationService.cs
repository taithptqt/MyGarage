using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class CustomerAuthorizationService : ICustomerAuthorizationService
{
    private readonly IGarageContext _context;

    public CustomerAuthorizationService(IGarageContext context)
    {
        _context = context;
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
            var customer = await _context.Customers
                .AsNoTracking()
                .Where(c => c.Id == customerId && c.IsDeleted != true)
                .Select(c => new { c.Id, c.Note })
                .FirstOrDefaultAsync(ct);

            if (customer == null)
            {
                return false;
            }

            // Check preferred branch tag
            var prefBranch = CustomerDomainRules.ExtractPreferredBranchId(customer.Note);
            if (prefBranch.HasValue && prefBranch.Value == branchId)
            {
                return true;
            }

            // Check service orders
            var hasBranchOrder = await _context.ServiceOrders
                .AsNoTracking()
                .AnyAsync(so => so.CustomerId == customerId && (so.OriginBranchId == branchId || so.ExecuteBranchId == branchId), ct);
            if (hasBranchOrder)
            {
                return true;
            }

            // Check appointments
            var hasBranchAppointment = await _context.Appointments
                .AsNoTracking()
                .AnyAsync(a => a.CustomerId == customerId && a.BranchId == branchId, ct);
            if (hasBranchAppointment)
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
