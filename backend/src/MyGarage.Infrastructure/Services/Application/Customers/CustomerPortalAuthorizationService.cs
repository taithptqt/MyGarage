using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class CustomerPortalAuthorizationService : ICustomerPortalAuthorizationService
{
    private readonly IGarageContext _context;

    public CustomerPortalAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<bool> CanAccessCustomerDashboardAsync(long customerId, UserActorContext actor, CancellationToken ct = default)
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
                return false;
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

            var prefBranch = CustomerDomainRules.ExtractPreferredBranchId(customer.Note);
            if (prefBranch.HasValue && prefBranch.Value == branchId)
            {
                return true;
            }

            var hasBranchOrder = await _context.ServiceOrders
                .AsNoTracking()
                .AnyAsync(so => so.CustomerId == customerId && (so.OriginBranchId == branchId || so.ExecuteBranchId == branchId), ct);
            if (hasBranchOrder)
            {
                return true;
            }

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

    public async Task<bool> CanManageCustomerVehicleAsync(long customerId, long vehicleId, UserActorContext actor, CancellationToken ct = default)
    {
        if (customerId <= 0 || vehicleId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (actor.IsAdmin || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            if (!actor.IsAdmin && !await CanAccessCustomerDashboardAsync(customerId, actor, ct)) return false;
            return await _context.CustomerVehicles.AsNoTracking()
                .AnyAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId && cv.IsDeleted != true, ct);
        }

        if (string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            if (actor.UserId.Value != customerId)
            {
                return false;
            }

            return await _context.CustomerVehicles
                .AsNoTracking()
                .AnyAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId && cv.IsDeleted != true && (cv.IsCurrentOwner ?? true), ct);
        }

        return false;
    }

    public async Task<bool> CanAccessConversationAsync(long conversationId, UserActorContext actor, CancellationToken ct = default)
    {
        if (conversationId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        var conv = await _context.Conversations
            .AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.Id, c.CustomerId, c.BranchId })
            .FirstOrDefaultAsync(ct);

        if (conv == null)
        {
            return false;
        }

        if (string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return conv.CustomerId == actor.UserId.Value;
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0)
            {
                return true;
            }

            return conv.BranchId == actor.BranchId.Value;
        }

        return false;
    }

    public Task<bool> CanAccessBranchChatInboxAsync(long branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (branchId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        if (actor.IsAdmin)
        {
            return Task.FromResult(true);
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(actor.BranchId.Value == branchId);
        }

        return Task.FromResult(false);
    }

    public async Task<bool> CanAccessNotificationAsync(long notificationId, UserActorContext actor, CancellationToken ct = default)
    {
        if (notificationId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (actor.IsAdmin || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.Id == notificationId && n.CustomerId == actor.UserId.Value, ct);
        }

        return false;
    }

    public Task<bool> CanManageMaintenanceRemindersAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
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
            if (!branchId.HasValue || !actor.BranchId.HasValue || actor.BranchId.Value <= 0)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(actor.BranchId.Value == branchId.Value);
        }

        return Task.FromResult(false);
    }

    public Task<bool> CanSubmitCustomerFeedbackAsync(long customerId, UserActorContext actor, CancellationToken ct = default)
    {
        if (customerId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        if (actor.IsAdmin || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(true);
        }

        if (string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(actor.UserId.Value == customerId);
        }

        return Task.FromResult(false);
    }
}
