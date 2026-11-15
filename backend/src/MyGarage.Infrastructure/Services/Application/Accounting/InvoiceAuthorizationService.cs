using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class InvoiceAuthorizationService : IInvoiceAuthorizationService
{
    private readonly IGarageContext _context;

    public InvoiceAuthorizationService(IGarageContext context)
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

    public Task<bool> CanViewInvoicesAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (IsStaff(actor))
        {
            if (branchId.HasValue && branchId.Value > 0 && actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
            {
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public async Task<bool> CanViewInvoiceAsync(long invoiceId, UserActorContext actor, CancellationToken ct = default)
    {
        if (invoiceId <= 0 || !actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new
            {
                i.Id,
                i.PayerId,
                i.PayerType,
                i.ServiceOrderId,
                i.RetailOrderId
            })
            .FirstOrDefaultAsync(ct);

        if (invoice == null) return false;

        if (IsCustomer(actor))
        {
            if (string.Equals(invoice.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) &&
                invoice.PayerId == actor.UserId.Value)
            {
                return true;
            }

            if (invoice.ServiceOrderId.HasValue)
            {
                var isSoOwner = await _context.ServiceOrders
                    .AsNoTracking()
                    .AnyAsync(so => so.Id == invoice.ServiceOrderId.Value && so.CustomerId == actor.UserId.Value, ct);
                if (isSoOwner) return true;
            }

            if (invoice.RetailOrderId.HasValue)
            {
                var isRoOwner = await _context.RetailOrders
                    .AsNoTracking()
                    .AnyAsync(ro => ro.Id == invoice.RetailOrderId.Value && ro.CustomerId == actor.UserId.Value, ct);
                if (isRoOwner) return true;
            }

            return false;
        }

        if (IsStaff(actor))
        {
            if (!actor.BranchId.HasValue) return true;

            if (invoice.ServiceOrderId.HasValue)
            {
                var soBranch = await _context.ServiceOrders
                    .AsNoTracking()
                    .Where(so => so.Id == invoice.ServiceOrderId.Value)
                    .Select(so => (long?)so.ExecuteBranchId)
                    .FirstOrDefaultAsync(ct);
                if (soBranch.HasValue && soBranch.Value != actor.BranchId.Value) return false;
            }
            else if (invoice.RetailOrderId.HasValue)
            {
                var roBranch = await _context.RetailOrders
                    .AsNoTracking()
                    .Where(ro => ro.Id == invoice.RetailOrderId.Value)
                    .Select(ro => (long?)ro.BranchId)
                    .FirstOrDefaultAsync(ct);
                if (roBranch.HasValue && roBranch.Value != actor.BranchId.Value) return false;
            }

            return true;
        }

        return false;
    }

    public Task<bool> CanCreateInvoiceAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (IsStaff(actor))
        {
            if (branchId.HasValue && branchId.Value > 0 && actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
            {
                return Task.FromResult(false);
            }

            var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
            var canCreate = roleCode.Contains("ADMIN")
                || roleCode.Contains("MANAGER")
                || roleCode.Contains("ACCOUNTANT")
                || roleCode.Contains("FINANCE")
                || roleCode.Contains("KE_TOAN")
                || roleCode.Contains("ADVISOR")
                || roleCode.Contains("RECEPTIONIST");

            return Task.FromResult(canCreate);
        }

        return Task.FromResult(false);
    }

    public Task<bool> CanExportTaxSummaryAsync(UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (IsStaff(actor))
        {
            var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
            var canExport = roleCode.Contains("ADMIN")
                || roleCode.Contains("ACCOUNTANT")
                || roleCode.Contains("FINANCE")
                || roleCode.Contains("KE_TOAN")
                || roleCode.Contains("MANAGER");

            return Task.FromResult(canExport);
        }

        return Task.FromResult(false);
    }
}
