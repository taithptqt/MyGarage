using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class VehicleAuthorizationService : IVehicleAuthorizationService
{
    private readonly IGarageContext _context;

    public VehicleAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<bool> CanAccessVehicleAsync(long vehicleId, UserActorContext actor, CancellationToken ct = default)
    {
        if (vehicleId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (actor.IsAdmin || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.CustomerVehicles
                .AsNoTracking()
                .AnyAsync(cv => cv.VehicleId == vehicleId && cv.CustomerId == actor.UserId.Value && cv.IsDeleted != true, ct);
        }

        return false;
    }

    public async Task<bool> CanModifyVehicleAsync(long vehicleId, UserActorContext actor, CancellationToken ct = default)
    {
        if (vehicleId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (actor.IsAdmin || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.CustomerVehicles
                .AsNoTracking()
                .AnyAsync(cv => cv.VehicleId == vehicleId && cv.CustomerId == actor.UserId.Value && cv.IsCurrentOwner == true && cv.IsDeleted != true, ct);
        }

        return false;
    }

    public async Task<bool> CanAccessCustomerVehicleAsync(long customerVehicleId, UserActorContext actor, CancellationToken ct = default)
    {
        if (customerVehicleId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (actor.IsAdmin || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.CustomerVehicles
                .AsNoTracking()
                .AnyAsync(cv => cv.Id == customerVehicleId && cv.CustomerId == actor.UserId.Value && cv.IsDeleted != true, ct);
        }

        return false;
    }

    public Task<bool> CanAdministerVehiclesAsync(UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(actor.IsAdmin || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase));
    }
}
