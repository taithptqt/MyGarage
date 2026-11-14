using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Retail;
using MyGarage.Application.Retail.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Services;

public sealed class RetailOrderApplicationService : IRetailOrderApplicationService
{
    private readonly IGarageContext _context;
    private readonly IRetailOrderAuthorizationService _authService;

    public RetailOrderApplicationService(
        IGarageContext context,
        IRetailOrderAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
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

    public async Task<RetailOrderDto> CheckoutAsync(
        RetailCheckoutInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanCheckoutRetailOrderAsync(input.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo đơn hàng bán lẻ tại chi nhánh này.");
        }

        var normalizedItems = (input.Items ?? new List<RetailCheckoutItemInput>())
            .Where(item => item.ProductId > 0 && item.Quantity > 0)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToList();

        if (normalizedItems.Count == 0)
        {
            throw new InvalidOperationException("Đơn hàng phải có ít nhất một sản phẩm hợp lệ.");
        }

        var branchExists = await _context.Branches
            .AsNoTracking()
            .AnyAsync(branch => branch.Id == input.BranchId && branch.IsDeleted != true, ct);

        if (!branchExists)
        {
            throw new InvalidOperationException("Chi nhánh đã chọn không hợp lệ hoặc không tồn tại.");
        }

        var productIds = normalizedItems.Select(item => item.ProductId).ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(product =>
                productIds.Contains(product.Id) &&
                product.IsDeleted != true &&
                product.IsActive != false)
            .ToListAsync(ct);

        if (products.Count != productIds.Count)
        {
            throw new InvalidOperationException("Một hoặc nhiều sản phẩm không hợp lệ hoặc đã ngừng kinh doanh.");
        }

        var availabilityMap = await RetailStockAvailabilityResolver.GetAvailableQuantitiesAsync(
            _context,
            productIds,
            input.BranchId);

        var productMap = products.ToDictionary(p => p.Id);
        foreach (var item in normalizedItems)
        {
            availabilityMap.TryGetValue(item.ProductId, out var availableQty);
            if (!RetailOrderLifecycleRules.CanFulfillStock(item.Quantity, availableQty))
            {
                var pName = productMap[item.ProductId].Name;
                throw new InvalidOperationException($"Sản phẩm '{pName}' không đủ tồn kho tại chi nhánh (yêu cầu {item.Quantity}, hiện có {availableQty}).");
            }
        }

        var orderItems = normalizedItems.Select(item =>
        {
            var product = productMap[item.ProductId];
            var unitPrice = product.RetailPrice ?? 0m;
            var taxRate = product.TaxRate ?? 0m;
            var amounts = RetailOrderLifecycleRules.CalculateLineAmounts(unitPrice, item.Quantity, taxRate);

            return new RetailOrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                CostPriceAtTime = product.CostPrice,
                UnitPriceAtTime = unitPrice,
                SubTotal = amounts.SubTotal,
                TaxRate = taxRate,
                TaxAmount = amounts.TaxAmount,
                FinalAmount = amounts.FinalAmount
            };
        }).ToList();

        var subTotal = orderItems.Sum(i => i.SubTotal);
        var taxAmount = orderItems.Sum(i => i.TaxAmount ?? 0m);

        Voucher? voucherToRedeem = null;
        decimal voucherDiscount = 0m;
        var now = VietnamTime.Now;

        if (!string.IsNullOrWhiteSpace(input.VoucherCode))
        {
            var normCode = input.VoucherCode.Trim();
            var candidateVoucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Code == normCode && v.IsDeleted != true, ct);

            if (candidateVoucher == null)
            {
                throw new InvalidOperationException($"Mã voucher '{input.VoucherCode}' không tồn tại.");
            }

            if (!string.Equals(candidateVoucher.Status, VoucherLifecycleRules.StatusActive, StringComparison.OrdinalIgnoreCase) || candidateVoucher.IsActive != true)
            {
                throw new InvalidOperationException($"Voucher '{candidateVoucher.Code}' không ở trạng thái khả dụng ({candidateVoucher.Status}).");
            }

            if (!VoucherLifecycleRules.IsDateRangeValid(now, candidateVoucher.StartDate, candidateVoucher.EndDate))
            {
                throw new InvalidOperationException($"Voucher '{candidateVoucher.Code}' đã hết hạn hoặc chưa đến ngày bắt đầu.");
            }

            if (!VoucherLifecycleRules.IsEligibleSpend(subTotal, candidateVoucher.MinOrderValue))
            {
                throw new InvalidOperationException($"Đơn hàng chưa đạt giá trị tối thiểu để áp dụng voucher ({candidateVoucher.MinOrderValue:N0}đ).");
            }

            if (!VoucherLifecycleRules.CanRedeemTotalQuota(candidateVoucher.UsageLimit, candidateVoucher.UsedCount))
            {
                throw new InvalidOperationException($"Voucher '{candidateVoucher.Code}' đã hết lượt sử dụng.");
            }

            var customerId = actor.UserId.GetValueOrDefault();
            if (candidateVoucher.MaxUsesPerCustomer > 0 && customerId > 0)
            {
                var customerUses = await _context.VoucherRedemptions
                    .AsNoTracking()
                    .CountAsync(r => r.VoucherId == candidateVoucher.Id && r.CustomerId == customerId && r.RedemptionStatus == "REDEEMED", ct);
                if (!VoucherLifecycleRules.CanRedeemCustomerQuota(candidateVoucher.MaxUsesPerCustomer, customerUses))
                {
                    throw new InvalidOperationException($"Bạn đã dùng hết hạn mức voucher này ({candidateVoucher.MaxUsesPerCustomer} lần).");
                }
            }

            voucherDiscount = VoucherLifecycleRules.CalculateDiscount(
                candidateVoucher.Type,
                candidateVoucher.Value,
                candidateVoucher.MaxDiscountAmount,
                subTotal);

            if (!VoucherLifecycleRules.CanRedeemBudget(candidateVoucher.BudgetLimit, candidateVoucher.BudgetUsed, voucherDiscount))
            {
                throw new InvalidOperationException($"Ngân sách cho voucher '{candidateVoucher.Code}' đã hết.");
            }

            voucherToRedeem = candidateVoucher;
        }

        var totals = RetailOrderLifecycleRules.CalculateOrderTotals(subTotal, taxAmount, voucherDiscount, 0m);
        var orderCode = $"RO{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(10, 99)}";

        var order = new RetailOrder
        {
            BranchId = input.BranchId,
            OrderCode = orderCode,
            CustomerId = actor.UserId.GetValueOrDefault(),
            SubTotal = totals.SubTotal,
            TierDiscountAmount = 0m,
            VoucherId = voucherToRedeem?.Id,
            VoucherDiscountAmount = voucherDiscount,
            TotalDiscountAmount = totals.TotalDiscount,
            TaxAmount = totals.TaxAmount,
            FinalAmount = totals.FinalAmount,
            Status = nameof(RetailOrderStatus.DRAFT),
            CustomerNotes = string.IsNullOrWhiteSpace(input.CustomerNotes) ? null : input.CustomerNotes.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.RetailOrders.Add(order);
                await _context.SaveChangesAsync(ct);

                foreach (var item in orderItems)
                {
                    item.RetailOrderId = order.Id;
                }
                _context.RetailOrderItems.AddRange(orderItems);
                await _context.SaveChangesAsync(ct);

                if (voucherToRedeem != null)
                {
                    var redemptionRecord = new VoucherRedemption
                    {
                        VoucherId = voucherToRedeem.Id,
                        VoucherCode = voucherToRedeem.Code,
                        CustomerId = actor.UserId,
                        BranchId = input.BranchId,
                        RetailOrderId = order.Id,
                        ValidationStatus = "VALIDATED",
                        RedemptionStatus = "REDEEMED",
                        EligibleAmount = totals.SubTotal,
                        DiscountAmount = voucherDiscount,
                        BudgetImpact = voucherDiscount,
                        AppliedBy = actor.UserId,
                        IdempotencyKey = $"ro-checkout-{order.OrderCode}",
                        AppliedAt = now,
                        RedeemedAt = now,
                        CreatedAt = now
                    };
                    _context.VoucherRedemptions.Add(redemptionRecord);

                    voucherToRedeem.UsedCount = (voucherToRedeem.UsedCount ?? 0) + 1;
                    voucherToRedeem.RedeemedCount += 1;
                    voucherToRedeem.BudgetUsed += voucherDiscount;
                    voucherToRedeem.UpdatedAt = now;
                    voucherToRedeem.UpdatedBy = actor.UserId;

                    var exhaustedByUsage = voucherToRedeem.UsageLimit.HasValue && voucherToRedeem.UsageLimit.Value > 0 && voucherToRedeem.UsedCount >= voucherToRedeem.UsageLimit.Value;
                    var exhaustedByBudget = voucherToRedeem.BudgetLimit.HasValue && voucherToRedeem.BudgetUsed >= voucherToRedeem.BudgetLimit.Value;
                    if (exhaustedByUsage || exhaustedByBudget)
                    {
                        voucherToRedeem.Status = VoucherLifecycleRules.StatusExhausted;
                        voucherToRedeem.IsActive = false;
                    }
                    await _context.SaveChangesAsync(ct);
                }

                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return MapToDto(order, orderItems, productMap);
    }

    public async Task<RetailOrderDto?> GetOrderByIdAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (!await _authService.CanViewRetailOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem chi tiết đơn hàng bán lẻ này.");
        }

        var order = await _context.RetailOrders
            .AsNoTracking()
            .Include(o => o.RetailOrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order == null) return null;

        var items = order.RetailOrderItems
            .OrderByDescending(i => i.Id)
            .Select(i => new RetailOrderItemDto(
                i.Id,
                i.ProductId,
                i.Product?.Name,
                i.Quantity,
                i.UnitPriceAtTime,
                i.SubTotal,
                i.TaxRate,
                i.TaxAmount,
                i.FinalAmount
            ))
            .ToList();

        return new RetailOrderDto(
            order.Id,
            order.OrderCode,
            order.BranchId,
            order.CustomerId,
            order.SubTotal,
            order.TierDiscountAmount,
            order.VoucherId,
            order.VoucherDiscountAmount,
            order.TotalDiscountAmount,
            order.TaxAmount,
            order.FinalAmount,
            order.Status,
            order.CustomerNotes,
            order.CreatedAt,
            items
        );
    }

    public async Task<IReadOnlyList<RetailOrderDto>> GetOrdersAsync(RetailOrderFilterInput filter, UserActorContext actor, CancellationToken ct = default)
    {
        var targetBranchId = filter.BranchId;
        if (!actor.IsAdmin && actor.BranchId.HasValue)
        {
            targetBranchId = actor.BranchId.Value;
        }

        if (!await _authService.CanViewRetailOrdersAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem danh sách đơn hàng bán lẻ.");
        }

        var query = _context.RetailOrders.AsNoTracking();

        if (targetBranchId.HasValue && targetBranchId.Value > 0)
        {
            query = query.Where(o => o.BranchId == targetBranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var norm = RetailOrderLifecycleRules.NormalizeStatus(filter.Status);
            query = query.Where(o => o.Status == norm);
        }

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var orders = await query
            .Include(o => o.RetailOrderItems)
                .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return orders.Select(order =>
        {
            var items = order.RetailOrderItems
                .Select(i => new RetailOrderItemDto(
                    i.Id,
                    i.ProductId,
                    i.Product?.Name,
                    i.Quantity,
                    i.UnitPriceAtTime,
                    i.SubTotal,
                    i.TaxRate,
                    i.TaxAmount,
                    i.FinalAmount
                ))
                .ToList();

            return new RetailOrderDto(
                order.Id,
                order.OrderCode,
                order.BranchId,
                order.CustomerId,
                order.SubTotal,
                order.TierDiscountAmount,
                order.VoucherId,
                order.VoucherDiscountAmount,
                order.TotalDiscountAmount,
                order.TaxAmount,
                order.FinalAmount,
                order.Status,
                order.CustomerNotes,
                order.CreatedAt,
                items
            );
        }).ToList();
    }

    public async Task<RetailCustomerOrderPageResultDto> GetCurrentCustomerOrdersPageAsync(
        RetailCustomerOrderPageInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var customerId = actor.UserId.GetValueOrDefault();
        if (customerId <= 0)
        {
            throw new UnauthorizedAccessException("Yêu cầu thông tin khách hàng hợp lệ.");
        }

        var baseQuery = _context.RetailOrders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId);

        var totalCount = await baseQuery.CountAsync(ct);
        var draftCount = await baseQuery.CountAsync(o => o.Status == nameof(RetailOrderStatus.DRAFT), ct);
        var confirmedCount = await baseQuery.CountAsync(o => o.Status == nameof(RetailOrderStatus.CONFIRMED), ct);
        var totalValue = await baseQuery.SumAsync(o => (decimal?)o.FinalAmount, ct) ?? 0m;

        var pageSize = Math.Clamp(input.PageSize, 1, 50);
        var page = Math.Max(1, input.Page);

        // If anchor order ID is specified, locate its page
        if (input.AnchorOrderId.HasValue && input.AnchorOrderId.Value > 0)
        {
            var orderedIds = await baseQuery
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .Select(o => o.Id)
                .ToListAsync(ct);

            var index = orderedIds.IndexOf(input.AnchorOrderId.Value);
            if (index >= 0)
            {
                page = (index / pageSize) + 1;
            }
        }

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var orders = await baseQuery
            .Include(o => o.RetailOrderItems)
                .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = orders.Select(order =>
        {
            var items = order.RetailOrderItems
                .Select(i => new RetailOrderItemDto(
                    i.Id,
                    i.ProductId,
                    i.Product?.Name,
                    i.Quantity,
                    i.UnitPriceAtTime,
                    i.SubTotal,
                    i.TaxRate,
                    i.TaxAmount,
                    i.FinalAmount
                ))
                .ToList();

            return new RetailOrderDto(
                order.Id,
                order.OrderCode,
                order.BranchId,
                order.CustomerId,
                order.SubTotal,
                order.TierDiscountAmount,
                order.VoucherId,
                order.VoucherDiscountAmount,
                order.TotalDiscountAmount,
                order.TaxAmount,
                order.FinalAmount,
                order.Status,
                order.CustomerNotes,
                order.CreatedAt,
                items
            );
        }).ToList();

        return new RetailCustomerOrderPageResultDto(
            dtos,
            totalCount,
            page,
            pageSize,
            totalPages,
            draftCount,
            confirmedCount,
            totalValue
        );
    }

    public async Task<bool> CancelOrderAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (!await _authService.CanCancelRetailOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền hủy đơn hàng bán lẻ này.");
        }

        var order = await _context.RetailOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order == null) return false;

        if (!RetailOrderLifecycleRules.CanCancel(order.Status))
        {
            throw new InvalidOperationException($"Không thể hủy đơn hàng ở trạng thái {order.Status}.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                order.Status = nameof(RetailOrderStatus.CANCELED);
                order.UpdatedAt = VietnamTime.Now;
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
                return true;
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<bool> DeleteOrderAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (!await _authService.CanDeleteRetailOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xóa đơn hàng bán lẻ này.");
        }

        var order = await _context.RetailOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order == null) return false;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.RetailOrders.Remove(order);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
                return true;
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    private static RetailOrderDto MapToDto(
        RetailOrder order,
        List<RetailOrderItem> items,
        Dictionary<long, Product> productMap)
    {
        var itemDtos = items.Select(i =>
        {
            productMap.TryGetValue(i.ProductId, out var product);
            return new RetailOrderItemDto(
                i.Id,
                i.ProductId,
                product?.Name,
                i.Quantity,
                i.UnitPriceAtTime,
                i.SubTotal,
                i.TaxRate,
                i.TaxAmount,
                i.FinalAmount
            );
        }).ToList();

        return new RetailOrderDto(
            order.Id,
            order.OrderCode,
            order.BranchId,
            order.CustomerId,
            order.SubTotal,
            order.TierDiscountAmount,
            order.VoucherId,
            order.VoucherDiscountAmount,
            order.TotalDiscountAmount,
            order.TaxAmount,
            order.FinalAmount,
            order.Status,
            order.CustomerNotes,
            order.CreatedAt,
            itemDtos
        );
    }
}
