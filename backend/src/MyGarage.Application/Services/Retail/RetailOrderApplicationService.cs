using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Retail;
using MyGarage.Application.Retail.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class RetailOrderApplicationService : IRetailOrderApplicationService
{
    private readonly IRetailOrderStore _store;
    private readonly IRetailOrderAuthorizationService _authService;

    public RetailOrderApplicationService(
        IRetailOrderStore store,
        IRetailOrderAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
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

        if (!await _store.ActiveBranchExistsAsync(input.BranchId, ct))
        {
            throw new InvalidOperationException("Chi nhánh đã chọn không hợp lệ hoặc không tồn tại.");
        }

        var productIds = normalizedItems.Select(item => item.ProductId).ToList();
        var products = await _store.GetActiveProductsByIdsAsync(productIds, ct);

        if (products.Count != productIds.Count)
        {
            throw new InvalidOperationException("Một hoặc nhiều sản phẩm không hợp lệ hoặc đã ngừng kinh doanh.");
        }

        var availabilityMap = await _store.GetAvailableStockQuantitiesAsync(productIds, input.BranchId, ct);

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
            var candidateVoucher = await _store.GetVoucherByCodeAsync(normCode, ct);

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
                var customerUses = await _store.CountCustomerRedeemedUsesAsync(candidateVoucher.Id, customerId, ct);
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

        await _store.ExecuteInTransactionAsync(async token =>
        {
            VoucherRedemption? redemptionRecord = null;
            if (voucherToRedeem != null)
            {
                redemptionRecord = new VoucherRedemption
                {
                    VoucherId = voucherToRedeem.Id,
                    VoucherCode = voucherToRedeem.Code,
                    CustomerId = actor.UserId,
                    BranchId = input.BranchId,
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
            }

            await _store.SaveCheckoutOrderAsync(order, orderItems, redemptionRecord, token);
            return true;
        }, ct);

        return MapToDto(order, orderItems, productMap);
    }

    public async Task<RetailOrderDto?> GetOrderByIdAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (!await _authService.CanViewRetailOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem chi tiết đơn hàng bán lẻ này.");
        }

        var order = await _store.GetOrderWithItemsByIdAsync(id, ct);
        return order == null ? null : MapOrderToDto(order, orderItemsDescending: true);
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

        var effectiveBranchId = targetBranchId.HasValue && targetBranchId.Value > 0 ? targetBranchId : null;
        var normStatus = !string.IsNullOrWhiteSpace(filter.Status) ? RetailOrderLifecycleRules.NormalizeStatus(filter.Status) : null;
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var orders = await _store.GetOrdersPageAsync(effectiveBranchId, normStatus, page, pageSize, ct);
        return orders.Select(o => MapOrderToDto(o, orderItemsDescending: false)).ToList();
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

        var pageSize = Math.Clamp(input.PageSize, 1, 50);
        var requestedPage = Math.Max(1, input.Page);

        var data = await _store.GetCustomerOrdersPageAsync(customerId, input.AnchorOrderId, requestedPage, pageSize, ct);
        var totalPages = data.TotalCount == 0 ? 0 : (int)Math.Ceiling(data.TotalCount / (double)pageSize);
        var dtos = data.Orders.Select(o => MapOrderToDto(o, orderItemsDescending: false)).ToList();

        return new RetailCustomerOrderPageResultDto(
            dtos,
            data.TotalCount,
            data.Page,
            pageSize,
            totalPages,
            data.DraftCount,
            data.ConfirmedCount,
            data.TotalValue
        );
    }

    public async Task<bool> CancelOrderAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (!await _authService.CanCancelRetailOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền hủy đơn hàng bán lẻ này.");
        }

        var order = await _store.GetOrderForMutationAsync(id, ct);
        if (order == null) return false;

        if (!RetailOrderLifecycleRules.CanCancel(order.Status))
        {
            throw new InvalidOperationException($"Không thể hủy đơn hàng ở trạng thái {order.Status}.");
        }

        return await _store.ExecuteInTransactionAsync(async token =>
        {
            order.Status = nameof(RetailOrderStatus.CANCELED);
            order.UpdatedAt = VietnamTime.Now;
            await _store.SaveChangesAsync(token);
            return true;
        }, ct);
    }

    public async Task<bool> DeleteOrderAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (!await _authService.CanDeleteRetailOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xóa đơn hàng bán lẻ này.");
        }

        var order = await _store.GetOrderForMutationAsync(id, ct);
        if (order == null) return false;

        return await _store.ExecuteInTransactionAsync(async token =>
        {
            await _store.RemoveOrderAsync(order, token);
            return true;
        }, ct);
    }

    private static RetailOrderDto MapOrderToDto(RetailOrder order, bool orderItemsDescending)
    {
        IEnumerable<RetailOrderItem> source = order.RetailOrderItems;
        if (orderItemsDescending)
        {
            source = source.OrderByDescending(i => i.Id);
        }

        var items = source.Select(i => new RetailOrderItemDto(
            i.Id,
            i.ProductId,
            i.Product?.Name,
            i.Quantity,
            i.UnitPriceAtTime,
            i.SubTotal,
            i.TaxRate,
            i.TaxAmount,
            i.FinalAmount
        )).ToList();

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
