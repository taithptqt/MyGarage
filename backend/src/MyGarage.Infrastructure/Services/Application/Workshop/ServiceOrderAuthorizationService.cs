using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class ServiceOrderAuthorizationService : IServiceOrderAuthorizationService
{
    private readonly IGarageContext _context;

    public ServiceOrderAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<bool> CanAccessServiceOrderAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        var meta = await _context.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => new { so.CustomerId, so.ExecuteBranchId, so.OriginBranchId })
            .FirstOrDefaultAsync(cancellationToken);

        if (meta == null)
        {
            return false;
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return !actor.BranchId.HasValue ||
                   actor.BranchId.Value == meta.ExecuteBranchId ||
                   actor.BranchId.Value == meta.OriginBranchId;
        }

        return actor.UserId.Value == meta.CustomerId;
    }

    public async Task<bool> CanMutateWorkshopAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var meta = await _context.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => new { so.ExecuteBranchId })
            .FirstOrDefaultAsync(cancellationToken);

        if (meta == null)
        {
            return false;
        }

        return !actor.BranchId.HasValue || actor.BranchId.Value == meta.ExecuteBranchId;
    }

    public async Task<bool> CanDispatchOrderAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (!await CanMutateWorkshopAsync(serviceOrderId, actor, cancellationToken))
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        return IsDispatcherOrForemanRole(actor.RoleCode);
    }

    public async Task<bool> CanLogTechnicianWorkAsync(long serviceOrderId, long serviceOrderDetailId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || serviceOrderDetailId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var meta = await _context.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => new { so.ExecuteBranchId })
            .FirstOrDefaultAsync(cancellationToken);

        if (meta == null || (actor.BranchId.HasValue && actor.BranchId.Value != meta.ExecuteBranchId))
        {
            return false;
        }

        if (IsDispatcherOrForemanRole(actor.RoleCode))
        {
            return true;
        }

        // Assigned technician check
        var isAssigned = await _context.ServiceStaffAssignments
            .AsNoTracking()
            .AnyAsync(a => a.ServiceOrderDetailId == serviceOrderDetailId && a.TechnicianId == actor.UserId.Value, cancellationToken);

        if (!isAssigned)
        {
            isAssigned = await _context.ServiceOrderDetails
                .AsNoTracking()
                .AnyAsync(d => d.Id == serviceOrderDetailId && d.TechnicianId == actor.UserId.Value, cancellationToken);
        }

        return isAssigned;
    }

    public async Task<bool> CanPerformQcInspectionAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var order = await _context.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => new { so.ExecuteBranchId })
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null)
        {
            return false;
        }

        if (!actor.IsAdmin && actor.BranchId.HasValue && actor.BranchId.Value != order.ExecuteBranchId)
        {
            return false;
        }

        // Separation of Duties: Inspector cannot be a technician who performed work on this order
        var workedOnOrder = await _context.ServiceStaffAssignments
            .AsNoTracking()
            .AnyAsync(a => a.ServiceOrderDetail.ServiceOrderId == serviceOrderId && a.TechnicianId == actor.UserId.Value, cancellationToken);

        if (!workedOnOrder)
        {
            workedOnOrder = await _context.ServiceOrderDetails
                .AsNoTracking()
                .AnyAsync(d => d.ServiceOrderId == serviceOrderId && d.TechnicianId == actor.UserId.Value, cancellationToken);
        }

        if (workedOnOrder)
        {
            return false; // Self-inspection forbidden
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        return IsQcOrForemanRole(actor.RoleCode);
    }

    public async Task<bool> CanPerformHandoverAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var order = await _context.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => new { so.ExecuteBranchId })
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null)
        {
            return false;
        }

        if (actor.BranchId.HasValue && actor.BranchId.Value != order.ExecuteBranchId)
        {
            return false;
        }

        return IsHandoverRole(actor.RoleCode);
    }

    public async Task<bool> CanDecideEditRequestAsync(long requestId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (requestId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestLog = await _context.AuditLogs
            .AsNoTracking()
            .Where(log => log.Id == requestId &&
                          log.Action == "REQUEST_EDIT" &&
                          (log.TableName == "SERVICE_ORDERS" || log.TableName == "SERVICEORDERS"))
            .Select(log => new { log.BranchId })
            .FirstOrDefaultAsync(cancellationToken);

        if (requestLog == null)
        {
            return false;
        }

        return !actor.BranchId.HasValue || requestLog.BranchId == actor.BranchId.Value;
    }

    private static bool IsDispatcherOrForemanRole(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return true;
        var normalized = roleCode.Trim().ToUpperInvariant();
        return normalized.Contains("DISPATCH") ||
               normalized.Contains("FOREMAN") ||
               normalized.Contains("TEAM_LEAD") ||
               normalized.Contains("QUAN_DOC") ||
               normalized.Contains("MANAGER") ||
               normalized.Contains("ADMIN") ||
               normalized.Contains("ADVISOR") ||
               normalized.Contains("CVDV");
    }

    private static bool IsQcOrForemanRole(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return true;
        var normalized = roleCode.Trim().ToUpperInvariant();
        return normalized.Contains("QC") ||
               normalized.Contains("INSPECTOR") ||
               normalized.Contains("FOREMAN") ||
               normalized.Contains("TEAM_LEAD") ||
               normalized.Contains("QUAN_DOC") ||
               normalized.Contains("MANAGER") ||
               normalized.Contains("ADMIN");
    }

    private static bool IsHandoverRole(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return true;
        var normalized = roleCode.Trim().ToUpperInvariant();
        return normalized.Contains("ADVISOR") ||
               normalized.Contains("CVDV") ||
               normalized.Contains("RECEPTIONIST") ||
               normalized.Contains("FRONT_DESK") ||
               normalized.Contains("CASHIER") ||
               normalized.Contains("THU_NGAN") ||
               normalized.Contains("MANAGER") ||
               normalized.Contains("ADMIN");
    }
}
