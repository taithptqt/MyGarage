using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Chatbot;

public sealed partial class EfChatbotStore
{
    public Task<ChatbotProductMatch?> FindBestActiveProductAsync(string loweredHint, CancellationToken ct = default)
    {
        return _context.Products
            .AsNoTracking()
            .Where(p => p.IsDeleted != true && (p.IsActive == null || p.IsActive == true))
            .OrderByDescending(p => p.Sku.ToLower() == loweredHint)
            .ThenByDescending(p => p.Name.ToLower() == loweredHint)
            .ThenByDescending(p => p.Sku.ToLower().Contains(loweredHint))
            .ThenByDescending(p => p.Name.ToLower().Contains(loweredHint))
            .Select(p => new ChatbotProductMatch
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                RetailPrice = p.RetailPrice,
                ServicePrice = p.ServicePrice
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotInventoryStockRow>> GetProductInventoryStockAsync(long productId, long? branchId, CancellationToken ct = default)
    {
        var inventories = _context.Inventories
            .AsNoTracking()
            .Where(i => i.ProductId == productId);

        if (branchId.HasValue)
        {
            inventories = inventories.Where(i => i.Warehouse.BranchId == branchId.Value);
        }

        return await inventories
            .Select(i => new ChatbotInventoryStockRow
            {
                Quantity = i.Quantity,
                Reserved = i.ReservedQuantity ?? 0m
            })
            .ToListAsync(ct);
    }

    public Task<ChatbotCustomerTierSnapshot?> GetCustomerTierSnapshotAsync(long customerId, CancellationToken ct = default)
    {
        return _context.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId && x.IsDeleted != true)
            .Select(x => new ChatbotCustomerTierSnapshot
            {
                Id = x.Id,
                TotalSpending = x.TotalSpending,
                TierName = x.Tier != null ? x.Tier.TierName : null,
                TierDiscount = x.Tier != null ? x.Tier.DiscountPercent : (decimal?)null,
                TierBenefits = x.Tier != null ? x.Tier.Benefits : null
            })
            .FirstOrDefaultAsync(ct);
    }

    public Task<ChatbotNextTierSnapshot?> GetNextCustomerTierAsync(decimal totalSpending, CancellationToken ct = default)
    {
        return _context.CustomerTiers
            .AsNoTracking()
            .Where(x => x.IsDeleted != true && x.MinSpending > totalSpending)
            .OrderBy(x => x.MinSpending)
            .Select(x => new ChatbotNextTierSnapshot
            {
                TierName = x.TierName,
                MinSpending = x.MinSpending
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotVoucherLookupRow>> GetDirectCustomerVouchersAsync(long customerId, DateTime nowUtc, CancellationToken ct = default)
    {
        return await _context.CustomerVouchers
            .AsNoTracking()
            .Where(cv =>
                cv.CustomerId == customerId
                && cv.IsUsed != true
                && cv.Voucher.IsDeleted != true
                && cv.Voucher.IsActive == true
                && cv.Voucher.Status != null
                && cv.Voucher.Status.ToUpper() == nameof(VoucherStatusCode.ACTIVE)
                && cv.Voucher.EndDate >= nowUtc)
            .Select(cv => new ChatbotVoucherLookupRow
            {
                VoucherId = cv.VoucherId,
                VoucherCode = cv.Voucher.Code,
                VoucherName = cv.Voucher.VoucherName,
                VoucherType = cv.Voucher.Type,
                VoucherValue = cv.Voucher.Value,
                MinOrderValue = cv.Voucher.MinOrderValue,
                MaxDiscountAmount = cv.Voucher.MaxDiscountAmount,
                StartDate = cv.Voucher.StartDate,
                EndDate = cv.Voucher.EndDate,
                MaxUsesPerCustomer = cv.Voucher.MaxUsesPerCustomer,
                Source = "DIRECT_ISSUED"
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<long>> GetExcludedCustomerVoucherIdsAsync(long customerId, DateTime nowUtc, CancellationToken ct = default)
    {
        return await _context.VoucherAssignments
            .AsNoTracking()
            .Where(a =>
                a.AssignmentType != null
                && a.AssignmentType.ToUpper() == "CUSTOMER"
                && a.TargetId == customerId
                && !a.IsInclude
                && (!a.EffectiveFrom.HasValue || a.EffectiveFrom <= nowUtc)
                && (!a.EffectiveTo.HasValue || a.EffectiveTo >= nowUtc))
            .Select(a => a.VoucherId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotVoucherLookupRow>> GetAssignedCustomerVouchersAsync(
        long customerId,
        DateTime nowUtc,
        IReadOnlyCollection<long> excludedVoucherIds,
        CancellationToken ct = default)
    {
        return await _context.VoucherAssignments
            .AsNoTracking()
            .Where(a =>
                a.AssignmentType != null
                && a.AssignmentType.ToUpper() == "CUSTOMER"
                && a.TargetId == customerId
                && a.IsInclude
                && (!a.EffectiveFrom.HasValue || a.EffectiveFrom <= nowUtc)
                && (!a.EffectiveTo.HasValue || a.EffectiveTo >= nowUtc)
                && a.Voucher.IsDeleted != true
                && a.Voucher.IsActive == true
                && a.Voucher.Status != null
                && a.Voucher.Status.ToUpper() == nameof(VoucherStatusCode.ACTIVE)
                && a.Voucher.EndDate >= nowUtc
                && !excludedVoucherIds.Contains(a.VoucherId))
            .Select(a => new ChatbotVoucherLookupRow
            {
                VoucherId = a.VoucherId,
                VoucherCode = a.Voucher.Code,
                VoucherName = a.Voucher.VoucherName,
                VoucherType = a.Voucher.Type,
                VoucherValue = a.Voucher.Value,
                MinOrderValue = a.Voucher.MinOrderValue,
                MaxDiscountAmount = a.Voucher.MaxDiscountAmount,
                StartDate = a.Voucher.StartDate,
                EndDate = a.Voucher.EndDate,
                MaxUsesPerCustomer = a.Voucher.MaxUsesPerCustomer,
                Source = "CUSTOMER_ASSIGNED"
            })
            .ToListAsync(ct);
    }

    public Task<Dictionary<long, int>> GetCustomerVoucherRedemptionCountsAsync(long customerId, CancellationToken ct = default)
    {
        return _context.VoucherRedemptions
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.ReversedAt == null)
            .GroupBy(x => x.VoucherId)
            .Select(group => new
            {
                VoucherId = group.Key,
                UsedCount = group.Count()
            })
            .ToDictionaryAsync(x => x.VoucherId, x => x.UsedCount, ct);
    }

    public Task<long?> GetCustomerTierIdAsync(long customerId, CancellationToken ct = default)
    {
        return _context.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == customerId && customer.IsDeleted != true)
            .Select(customer => customer.TierId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<ChatbotVehicleContextSnapshot?> GetVehicleContextAsync(long vehicleId, CancellationToken ct = default)
    {
        return _context.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.Id == vehicleId && vehicle.IsDeleted != true)
            .Select(vehicle => new ChatbotVehicleContextSnapshot
            {
                VehicleId = vehicle.Id,
                VehicleModelId = vehicle.ModelId,
                VehicleBrandId = vehicle.Model.BrandId
            })
            .FirstOrDefaultAsync(ct);
    }

    public Task<List<Voucher>> GetCandidateVouchersForResolverAsync(long customerId, DateTime nowLocal, CancellationToken ct = default)
    {
        return _context.Vouchers
            .AsNoTracking()
            .Where(voucher => voucher.IsDeleted != true)
            .Where(voucher => voucher.IsActive != false)
            .Where(voucher => voucher.Status == nameof(VoucherStatusCode.ACTIVE))
            .Where(voucher => voucher.StartDate <= nowLocal && voucher.EndDate >= nowLocal)
            .Where(voucher => voucher.BudgetLimit == null || voucher.BudgetUsed < voucher.BudgetLimit)
            .Where(voucher => !voucher.VoucherAssignments.Any(a =>
                a.AssignmentType == "CUSTOMER" &&
                !a.IsInclude &&
                a.TargetId == customerId &&
                (!a.EffectiveFrom.HasValue || a.EffectiveFrom.Value <= nowLocal) &&
                (!a.EffectiveTo.HasValue || a.EffectiveTo.Value >= nowLocal)))
            .Include(voucher => voucher.VoucherAssignments)
            .Include(voucher => voucher.VoucherUsageRules)
            .Include(voucher => voucher.VoucherConflictRules)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotToolVehicleRow>> GetCustomerVehiclesForToolAsync(
        long customerId,
        bool includeInactiveOwnership,
        int take,
        CancellationToken ct = default)
    {
        var query = _context.CustomerVehicles
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == customerId &&
                x.IsDeleted != true &&
                x.Vehicle.IsDeleted != true);
        if (!includeInactiveOwnership)
        {
            query = query.Where(x => x.IsCurrentOwner != false);
        }

        return await query
            .OrderBy(x => x.Vehicle.LicensePlate)
            .Take(take)
            .Select(x => new ChatbotToolVehicleRow
            {
                VehicleId = x.VehicleId,
                LicensePlate = x.Vehicle.LicensePlate,
                ModelName = x.Vehicle.Model.ModelName,
                BrandName = x.Vehicle.Model.Brand.Name
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotToolBranchRow>> GetBranchesForToolAsync(bool onlyActive, CancellationToken ct = default)
    {
        var query = _context.Branches.AsNoTracking().Where(x => x.IsDeleted != true);
        if (onlyActive)
        {
            query = query.Where(x => x.Status != null && x.Status.ToUpper() == "ACTIVE");
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new ChatbotToolBranchRow
            {
                BranchId = x.Id,
                BranchName = x.Name,
                Status = x.Status,
                Address = x.Address,
                Hotline = x.Hotline
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotToolServiceRow>> SearchStandardServicesForToolAsync(string? keyword, int take, CancellationToken ct = default)
    {
        var query = _context.StandardServices.AsNoTracking().Where(x => x.IsDeleted != true && x.IsActive == true);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var token = keyword.Trim().ToUpperInvariant();
            query = query.Where(x =>
                (x.Code != null && x.Code.ToUpper().Contains(token)) ||
                (x.Name != null && x.Name.ToUpper().Contains(token)));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(take)
            .Select(x => new ChatbotToolServiceRow
            {
                ServiceId = x.Id,
                ServiceCode = x.Code,
                ServiceName = x.Name,
                BasePrice = x.BasePrice,
                StandardMinutes = x.StandardMinutes
            })
            .ToListAsync(ct);
    }

    public Task<ChatbotCustomerContact?> GetCustomerContactAsync(long customerId, CancellationToken ct = default)
    {
        return _context.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId && x.IsDeleted != true)
            .Select(x => new ChatbotCustomerContact
            {
                FullName = x.FullName,
                Phone = x.Phone
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotToolServiceHistoryRow>> GetCustomerServiceHistoryForToolAsync(long customerId, int take, CancellationToken ct = default)
    {
        return await _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .Take(take)
            .Select(x => new ChatbotToolServiceHistoryRow
            {
                OrderId = x.Id,
                OrderCode = x.Code,
                Status = x.Status,
                PaymentStatus = x.PaymentStatus,
                FinalAmount = x.FinalAmount,
                CreatedAt = x.CreatedAt,
                VehicleId = x.VehicleId,
                LicensePlate = x.Vehicle.LicensePlate,
                VehicleModel = x.Vehicle.Model.ModelName
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotToolServiceOrderStatusRow>> GetCustomerServiceOrderStatusForToolAsync(
        long customerId,
        long? orderId,
        string? orderCode,
        int take,
        CancellationToken ct = default)
    {
        var query = _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId);

        if (orderId.HasValue && orderId.Value > 0)
        {
            query = query.Where(x => x.Id == orderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(orderCode))
        {
            var normalizedCode = orderCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.Code.ToUpper().Contains(normalizedCode));
        }

        return await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
            .Take(take)
            .Select(x => new ChatbotToolServiceOrderStatusRow
            {
                OrderId = x.Id,
                OrderCode = x.Code,
                Status = x.Status,
                QcStatus = x.Qcstatus,
                PaymentStatus = x.PaymentStatus,
                PromisedHandover = x.PromisedHandoverTime,
                ActualHandover = x.ActualHandover,
                UpdatedAt = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotToolInvoiceStatusRow>> GetCustomerInvoiceStatusForToolAsync(
        long customerId,
        long? invoiceId,
        string? orderCode,
        int take,
        CancellationToken ct = default)
    {
        var query = _context.Invoices
            .AsNoTracking()
            .Where(x =>
                (x.ServiceOrderId != null && x.ServiceOrder != null && x.ServiceOrder.CustomerId == customerId) ||
                (x.RetailOrderId != null && x.RetailOrder != null && x.RetailOrder.CustomerId == customerId));

        if (invoiceId.HasValue && invoiceId.Value > 0)
        {
            query = query.Where(x => x.Id == invoiceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(orderCode))
        {
            var normalizedCode = orderCode.Trim().ToUpperInvariant();
            query = query.Where(x =>
                (x.ServiceOrder != null && x.ServiceOrder.Code.ToUpper().Contains(normalizedCode)) ||
                (x.RetailOrder != null && x.RetailOrder.OrderCode.ToUpper().Contains(normalizedCode)));
        }

        return await query
            .OrderByDescending(x => x.IssuedAt ?? x.CreatedAt ?? DateTime.MinValue)
            .Take(take)
            .Select(x => new ChatbotToolInvoiceStatusRow
            {
                InvoiceId = x.Id,
                ServiceOrderCode = x.ServiceOrder != null ? x.ServiceOrder.Code : null,
                RetailOrderCode = x.RetailOrder != null ? x.RetailOrder.OrderCode : null,
                TotalAmount = x.TotalAmount,
                PaidAmount = x.PaidAmount,
                PaymentStatus = x.PaymentStatus,
                IssuedAt = x.IssuedAt,
                RemainingAmount = (x.TotalAmount ?? 0m) - (x.PaidAmount ?? 0m)
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotToolWarrantyRow>> GetCustomerWarrantyHistoryForToolAsync(
        long customerId,
        long? vehicleId,
        int take,
        CancellationToken ct = default)
    {
        var query = _context.ServiceOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.ServiceOrder.CustomerId == customerId &&
                (
                    x.IsWarrantyItem == true ||
                    (x.StandardService != null &&
                     ((x.StandardService.WarrantyMonths ?? 0) > 0 || (x.StandardService.WarrantyKm ?? 0) > 0))
                ));

        if (vehicleId.HasValue && vehicleId.Value > 0)
        {
            query = query.Where(x => x.ServiceOrder.VehicleId == vehicleId.Value);
        }

        return await query
            .OrderByDescending(x => x.ServiceOrder.CreatedAt ?? DateTime.MinValue)
            .Take(take)
            .Select(x => new ChatbotToolWarrantyRow
            {
                ServiceOrderId = x.ServiceOrderId,
                ServiceOrderCode = x.ServiceOrder.Code,
                VehicleId = x.ServiceOrder.VehicleId,
                LicensePlate = x.ServiceOrder.Vehicle.LicensePlate,
                ItemName = x.ItemName,
                IsWarrantyItem = x.IsWarrantyItem,
                WarrantyMonths = x.StandardService != null ? x.StandardService.WarrantyMonths : null,
                WarrantyKm = x.StandardService != null ? x.StandardService.WarrantyKm : null,
                ServiceOrderStatus = x.ServiceOrder.Status,
                CreatedAt = x.ServiceOrder.CreatedAt
            })
            .ToListAsync(ct);
    }

    public Task<bool> CustomerOwnsVehicleAsync(long customerId, long vehicleId, CancellationToken ct = default)
    {
        return _context.CustomerVehicles
            .AsNoTracking()
            .AnyAsync(x =>
                x.CustomerId == customerId &&
                x.VehicleId == vehicleId &&
                x.IsDeleted != true &&
                x.IsCurrentOwner != false,
                ct);
    }

    public async Task<IReadOnlyList<long>> GetActiveBranchIdsAsync(IReadOnlyCollection<long> branchIds, CancellationToken ct = default)
    {
        return await _context.Branches
            .AsNoTracking()
            .Where(x =>
                branchIds.Contains(x.Id) &&
                x.IsDeleted != true &&
                x.Status != null &&
                x.Status.ToUpper() == "ACTIVE")
            .Select(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotToolServiceDraftRow>> GetActiveStandardServicesByIdsAsync(IReadOnlyCollection<long> serviceIds, CancellationToken ct = default)
    {
        return await _context.StandardServices
            .AsNoTracking()
            .Where(x => serviceIds.Contains(x.Id) && x.IsDeleted != true && x.IsActive == true)
            .Select(x => new ChatbotToolServiceDraftRow
            {
                ServiceId = x.Id,
                ServiceCode = x.Code,
                ServiceName = x.Name,
                BasePrice = x.BasePrice
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotToolProductRow>> GetActiveProductsByIdsAsync(IReadOnlyCollection<long> productIds, CancellationToken ct = default)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.IsDeleted != true && x.IsActive == true)
            .Select(x => new ChatbotToolProductRow
            {
                Id = x.Id,
                Sku = x.Sku,
                Name = x.Name,
                UnitPrice = x.RetailPrice ?? 0m
            })
            .ToListAsync(ct);
    }

    public Task<Dictionary<long, decimal>> GetAvailableStockByProductIdsAsync(long branchId, IReadOnlyCollection<long> productIds, CancellationToken ct = default)
    {
        return _context.Inventories
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId) && x.Warehouse.BranchId == branchId)
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                productId = g.Key,
                availableStock = g.Sum(x => x.Quantity - (x.ReservedQuantity ?? 0m))
            })
            .ToDictionaryAsync(x => x.productId, x => x.availableStock, ct);
    }

}
