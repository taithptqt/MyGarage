using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.CustomerPortal;
using MyGarage.Application.CustomerPortal.DTOs;
using MyGarage.Common;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;
using MyGarage.Support;

namespace MyGarage.Infrastructure.CustomerPortal;

public sealed partial class EfCustomerPortalStore : ICustomerPortalStore
{
    private readonly IGarageContext _context;

    public EfCustomerPortalStore(IGarageContext context)
    {
        _context = context;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _context.Database.BeginTransactionAsync();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return null;
            }
            throw;
        }
    }

    private async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await action(ct);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    private static readonly HashSet<string> TerminalOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(ServiceOrderLifecycleStatus.CLOSED),
        nameof(ServiceOrderLifecycleStatus.CANCELED),
        "CANCELED",
        "DELIVERED"
    };

    private static readonly HashSet<string> CompletedForMaintenance = new(StringComparer.OrdinalIgnoreCase)
    {
        "CLOSED", "READY", "COMPLETED", "DONE"
    };

    private static readonly HashSet<string> ActiveAppointmentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING", "CONFIRMED", "ARRIVED", "ARRIVED_LATE", "LATE"
    };

    private static readonly HashSet<string> TerminalWorkSlotStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(WorkSlotStatus.COMPLETED),
        nameof(WorkSlotStatus.CANCELED),
        "CANCELED"
    };

    private static readonly HashSet<string> DashboardOwnedAssignmentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(ServiceProgressStatus.PENDING),
        nameof(ServiceProgressStatus.IN_PROGRESS),
        nameof(ServiceProgressStatus.PAUSED)
    };

    public async Task<(bool Exists, string? Note)> GetActiveCustomerNoteAsync(long customerId, CancellationToken ct = default)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId && c.IsDeleted != true)
            .Select(c => new { c.Id, c.Note })
            .FirstOrDefaultAsync(ct);

        return customer == null ? (false, null) : (true, customer.Note);
    }

    public async Task<bool> HasBranchServiceOrderAsync(long customerId, long branchId, CancellationToken ct = default)
    {
        return await _context.ServiceOrders
            .AsNoTracking()
            .AnyAsync(so => so.CustomerId == customerId && (so.OriginBranchId == branchId || so.ExecuteBranchId == branchId), ct);
    }

    public async Task<bool> HasBranchAppointmentAsync(long customerId, long branchId, CancellationToken ct = default)
    {
        return await _context.Appointments
            .AsNoTracking()
            .AnyAsync(a => a.CustomerId == customerId && a.BranchId == branchId, ct);
    }

    public async Task<bool> CustomerHasVehicleAsync(long customerId, long vehicleId, bool requireCurrentOwner, CancellationToken ct = default)
    {
        if (requireCurrentOwner)
        {
            return await _context.CustomerVehicles.AsNoTracking()
                .AnyAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId && cv.IsDeleted != true && (cv.IsCurrentOwner ?? true), ct);
        }

        return await _context.CustomerVehicles.AsNoTracking()
            .AnyAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId && cv.IsDeleted != true, ct);
    }

    public async Task<CustomerPortalConversationAccessInfo?> GetConversationAccessInfoAsync(long conversationId, CancellationToken ct = default)
    {
        return await _context.Conversations
            .AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new CustomerPortalConversationAccessInfo(c.Id, c.CustomerId, c.BranchId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> IsCustomerNotificationAsync(long notificationId, long customerId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .AnyAsync(n => n.Id == notificationId && n.CustomerId == customerId, ct);
    }

    public async Task<(IReadOnlyList<CustomerPortalNotificationDto> Items, int UnreadCount, int TotalCount)> GetNotificationsAsync(
        long customerId,
        bool? unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _context.Notifications.AsNoTracking()
            .Where(n => n.CustomerId == customerId);
        if (unreadOnly == true)
            q = q.Where(n => n.IsRead != true);

        var unreadCount = await _context.Notifications.AsNoTracking()
            .CountAsync(n => n.CustomerId == customerId && n.IsRead != true, ct);

        var totalCount = await q.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= totalCount)
            return (Array.Empty<CustomerPortalNotificationDto>(), unreadCount, totalCount);

        var list = await q
            .OrderByDescending(n => n.SentAt)
            .ThenByDescending(n => n.Id)
            .Skip((int)offset).Take(pageSize)
            .Select(n => new CustomerPortalNotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                IsRead = n.IsRead == true,
                SentAt = n.SentAt,
                LinkAction = n.LinkAction,
                DerivedType = ""
            })
            .ToListAsync(ct);

        return (list, unreadCount, totalCount);
    }

    public async Task<bool> MarkNotificationReadAsync(long customerId, long notificationId, CancellationToken ct = default)
    {
        var n = await _context.Notifications.FirstOrDefaultAsync(
            x => x.Id == notificationId && x.CustomerId == customerId, ct);
        if (n == null) return false;
        n.IsRead = true;
        await ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
        return true;
    }

    public async Task<int> MarkAllNotificationsReadAsync(long customerId, CancellationToken ct = default)
    {
        if (_context.Database.IsRelational())
        {
            return await _context.Notifications
                .Where(n => n.CustomerId == customerId && n.IsRead != true)
                .ExecuteUpdateAsync(update => update.SetProperty(n => n.IsRead, true), ct);
        }

        var rows = await _context.Notifications
            .Where(n => n.CustomerId == customerId && n.IsRead != true)
            .ToListAsync(ct);
        foreach (var n in rows)
            n.IsRead = true;
        if (rows.Count > 0)
            await _context.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<CustomerPortalVehicleLookupDto> GetVehicleLookupsAsync(CancellationToken ct = default)
    {
        var brands = await _context.VehicleBrands.AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .OrderBy(b => b.Name)
            .Select(b => new CustomerPortalBrandLookupDto { Id = b.Id, Name = b.Name })
            .ToListAsync(ct);

        var models = await _context.VehicleModels.AsNoTracking()
            .Where(m => m.IsDeleted != true)
            .OrderBy(m => m.ModelName)
            .Select(m => new CustomerPortalModelLookupDto
            {
                Id = m.Id,
                BrandId = m.BrandId,
                BrandName = m.Brand.Name,
                ModelName = m.ModelName
            })
            .ToListAsync(ct);

        return new CustomerPortalVehicleLookupDto { Brands = brands, Models = models };
    }

    public async Task<CustomerPortalServiceOrderLinkContextDto?> GetServiceOrderLinkContextAsync(
        long customerId,
        long serviceOrderId,
        CancellationToken ct = default)
    {
        var row = await (
            from so in _context.ServiceOrders.AsNoTracking()
            join cv in _context.CustomerVehicles.AsNoTracking() on so.VehicleId equals cv.VehicleId
            where so.Id == serviceOrderId
            where so.CustomerId == customerId
            where cv.CustomerId == customerId
            where cv.IsDeleted != true
            where cv.IsCurrentOwner != false
            select new
            {
                so.Id,
                so.VehicleId
            })
            .FirstOrDefaultAsync(ct);

        if (row == null)
            return null;

        return new CustomerPortalServiceOrderLinkContextDto
        {
            ServiceOrderId = row.Id,
            VehicleId = row.VehicleId,
            SuggestedTab = "history",
            TargetPath = $"/my-vehicles/{row.VehicleId}?tab=history&serviceOrderId={row.Id}"
        };
    }

}
