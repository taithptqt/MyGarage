using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Domain.Rules;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Controllers
{
    [Route("api/retail-orders")]
    [Route("api/v1/retail-orders")]
    [ApiController]
    public class RetailOrderController : ControllerBase
    {
        private readonly IRetailOrderService _retailOrderService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _db;

        public RetailOrderController(
            IRetailOrderService retailOrderService,
            IMapper mapper,
            IGarageContext db)
        {
            _retailOrderService = retailOrderService;
            _mapper = mapper;
            _db = db;
        }

        private static string GenerateRetailOrderCode()
        {
            return $"RO{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(10, 99)}";
        }

        private static decimal RoundCurrency(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        [Authorize(Roles = "Staff")]
        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<RetailOrderResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var query = _db.RetailOrders.AsNoTracking().AsQueryable();
            if (scopedBranchId.HasValue)
            {
                query = query.Where(order => order.BranchId == scopedBranchId.Value);
            }

            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var paged = offset >= total
                ? new List<RetailOrder>()
                : await query.OrderByDescending(order => order.Id).Skip((int)offset).Take(normalizedSize).ToListAsync();
            var dtos = _mapper.Map<List<RetailOrderResponseDto>>(paged);
            return Ok(PagedEnvelope<RetailOrderResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [Authorize]
        [HttpGet("customers/current")]
        public async Task<ActionResult<PagedEnvelope<RetailOrderResponseDto>>> GetMine(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var customerId = this.GetCurrentUserId();
            if (customerId == null)
            {
                return Unauthorized();
            }

            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var query = _db.RetailOrders
                .AsNoTracking()
                .Where(order => order.CustomerId == customerId.Value)
                .OrderByDescending(order => order.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(order => order.Id);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var paged = offset >= total
                ? new List<RetailOrder>()
                : await query.Skip((int)offset).Take(normalizedSize).ToListAsync();
            var dtos = _mapper.Map<List<RetailOrderResponseDto>>(paged);
            return Ok(PagedEnvelope<RetailOrderResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("customers/current/page")]
        public async Task<IActionResult> GetMinePage([FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] long? anchorId)
        {
            var customerId = this.GetCurrentUserId();
            if (!customerId.HasValue) return Unauthorized();
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize, 20, 100);
            var query = _db.RetailOrders.AsNoTracking().Where(order => order.CustomerId == customerId.Value);

            var summary = await query.GroupBy(_ => 1).Select(group => new
            {
                Total = group.Count(),
                DraftCount = group.Count(order => order.Status == null || order.Status == "" || order.Status.ToUpper() == "DRAFT"),
                ConfirmedCount = group.Count(order => order.Status != null && order.Status.ToUpper() == "CONFIRMED"),
                TotalValue = group.Sum(order => order.FinalAmount)
            }).FirstOrDefaultAsync();
            var total = summary?.Total ?? 0;

            if (anchorId > 0)
            {
                var anchor = await query.Where(order => order.Id == anchorId).Select(order => new { order.Id, order.CreatedAt }).FirstOrDefaultAsync();
                if (anchor != null)
                {
                    var anchorDate = anchor.CreatedAt ?? DateTime.MinValue;
                    var preceding = await query.CountAsync(order => (order.CreatedAt ?? DateTime.MinValue) > anchorDate ||
                        ((order.CreatedAt ?? DateTime.MinValue) == anchorDate && order.Id > anchor.Id));
                    normalizedPage = preceding / normalizedSize + 1;
                }
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)normalizedSize));
            normalizedPage = Math.Min(normalizedPage, totalPages);
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var items = await query.OrderByDescending(order => order.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(order => order.Id).Skip((int)offset).Take(normalizedSize).ToListAsync();
            return Ok(new
            {
                items = _mapper.Map<List<RetailOrderResponseDto>>(items),
                page = normalizedPage, pageSize = normalizedSize, total,
                draftCount = summary?.DraftCount ?? 0, confirmedCount = summary?.ConfirmedCount ?? 0,
                totalValue = summary?.TotalValue ?? 0m
            });
        }

        [Authorize]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<RetailOrderResponseDto>> GetById(long id)
        {
            var item = await _db.RetailOrders
                .AsNoTracking()
                .Include(order => order.RetailOrderItems)
                    .ThenInclude(orderItem => orderItem.Product)
                .FirstOrDefaultAsync(order => order.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            if (this.IsStaffUser())
            {
                if (!this.IsAdminStaff() && item.BranchId != this.GetCurrentBranchId())
                {
                    return Forbid();
                }
            }
            else
            {
                var currentCustomerId = this.GetCurrentUserId();
                if (!currentCustomerId.HasValue || item.CustomerId != currentCustomerId.Value)
                {
                    return Forbid();
                }
            }

            var dto = _mapper.Map<RetailOrderResponseDto>(item);
            dto.Items = _mapper.Map<List<RetailOrderItemResponseDto>>(
                item.RetailOrderItems
                    .OrderByDescending(orderItem => orderItem.Id)
                    .ToList());

            return Ok(dto);
        }

        [Authorize]
        [HttpPost("checkout")]
        public async Task<ActionResult<RetailOrderResponseDto>> Checkout([FromBody] RetailCheckoutRequestDto request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Retail checkout data is required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customerId = this.GetCurrentUserId();
            if (customerId == null)
            {
                return Unauthorized();
            }

            if (request.BranchId <= 0)
            {
                return BadRequest(new { message = "BranchId must be greater than 0." });
            }

            var normalizedItems = (request.Items ?? new List<RetailCheckoutItemRequestDto>())
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
                return BadRequest(new { message = "Retail order must contain at least one item." });
            }

            var branchExists = await _db.Branches
                .AsNoTracking()
                .AnyAsync(branch => branch.Id == request.BranchId && branch.IsDeleted != true);

            if (!branchExists)
            {
                return BadRequest(new
                {
                    message = "Selected branch is invalid.",
                    branchId = request.BranchId
                });
            }

            var productIds = normalizedItems.Select(item => item.ProductId).ToList();
            var products = await _db.Products
                .AsNoTracking()
                .Where(product =>
                    productIds.Contains(product.Id) &&
                    product.IsDeleted != true &&
                    product.IsActive != false)
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                var availableProductIds = products.Select(product => product.Id).ToHashSet();
                var missingProductIds = productIds
                    .Where(productId => !availableProductIds.Contains(productId))
                    .OrderBy(productId => productId)
                    .ToList();

                return BadRequest(new
                {
                    message = "One or more selected retail products are invalid or inactive.",
                    productIds = missingProductIds
                });
            }

            var productMap = products.ToDictionary(product => product.Id);
            var availabilityMap = await RetailStockAvailabilityResolver.GetAvailableQuantitiesAsync(
                _db,
                productIds,
                request.BranchId);

            var stockIssues = normalizedItems
                .Select(item =>
                {
                    var product = productMap[item.ProductId];
                    availabilityMap.TryGetValue(item.ProductId, out var availableQuantity);

                    return new
                    {
                        productId = item.ProductId,
                        productName = product.Name,
                        requestedQuantity = item.Quantity,
                        availableQuantity
                    };
                })
                .Where(item => item.requestedQuantity > item.availableQuantity)
                .OrderBy(item => item.productId)
                .ToList();

            if (stockIssues.Count > 0)
            {
                return BadRequest(new
                {
                    message = "Một hoặc nhiều phụ tùng không còn đủ tồn kho tại chi nhánh đã chọn.",
                    items = stockIssues
                });
            }

            var retailOrderItems = normalizedItems
                .Select(item =>
                {
                    var product = productMap[item.ProductId];
                    var unitPrice = RoundCurrency(product.RetailPrice ?? 0m);
                    var subTotal = RoundCurrency(unitPrice * item.Quantity);
                    var taxRate = product.TaxRate ?? 0m;
                    var taxAmount = RoundCurrency(subTotal * taxRate / 100m);
                    var finalAmount = RoundCurrency(subTotal + taxAmount);

                    return new RetailOrderItem
                    {
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        CostPriceAtTime = product.CostPrice,
                        UnitPriceAtTime = unitPrice,
                        SubTotal = subTotal,
                        TaxRate = taxRate,
                        TaxAmount = taxAmount,
                        FinalAmount = finalAmount
                    };
                })
                .ToList();

            var subTotalAmount = RoundCurrency(retailOrderItems.Sum(item => item.SubTotal));
            var taxAmountTotal = RoundCurrency(retailOrderItems.Sum(item => item.TaxAmount ?? 0m));
            var now = VietnamTime.Now;

            Voucher? voucherToRedeem = null;
            decimal voucherDiscount = 0m;

            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                var normCode = request.VoucherCode.Trim().ToUpperInvariant();
                var candidateVoucher = await _db.Vouchers
                    .FirstOrDefaultAsync(v => v.Code == normCode && v.IsDeleted != true);

                if (candidateVoucher == null)
                {
                    return BadRequest(new { message = $"Mã voucher '{request.VoucherCode}' không tồn tại." });
                }

                if (!string.Equals(candidateVoucher.Status, VoucherLifecycleRules.StatusActive, StringComparison.OrdinalIgnoreCase) || candidateVoucher.IsActive != true)
                {
                    return BadRequest(new { message = $"Voucher '{candidateVoucher.Code}' không ở trạng thái khả dụng ({candidateVoucher.Status})." });
                }

                if (!VoucherLifecycleRules.IsDateRangeValid(now, candidateVoucher.StartDate, candidateVoucher.EndDate))
                {
                    return BadRequest(new { message = $"Voucher '{candidateVoucher.Code}' đã hết hạn hoặc chưa đến ngày bắt đầu." });
                }

                if (!VoucherLifecycleRules.IsEligibleSpend(subTotalAmount, candidateVoucher.MinOrderValue))
                {
                    return BadRequest(new { message = $"Đơn hàng chưa đạt giá trị tối thiểu để áp dụng voucher ({candidateVoucher.MinOrderValue:N0}đ)." });
                }

                if (!VoucherLifecycleRules.CanRedeemTotalQuota(candidateVoucher.UsageLimit, candidateVoucher.UsedCount))
                {
                    return BadRequest(new { message = $"Voucher '{candidateVoucher.Code}' đã hết lượt sử dụng." });
                }

                if (candidateVoucher.MaxUsesPerCustomer > 0)
                {
                    var customerUses = await _db.VoucherRedemptions
                        .AsNoTracking()
                        .CountAsync(r => r.VoucherId == candidateVoucher.Id && r.CustomerId == customerId.Value && r.RedemptionStatus == "REDEEMED");
                    if (!VoucherLifecycleRules.CanRedeemCustomerQuota(candidateVoucher.MaxUsesPerCustomer, customerUses))
                    {
                        return BadRequest(new { message = $"Bạn đã dùng hết hạn mức của voucher này ({candidateVoucher.MaxUsesPerCustomer} lần)." });
                    }
                }

                voucherDiscount = VoucherLifecycleRules.CalculateDiscount(
                    candidateVoucher.Type,
                    candidateVoucher.Value,
                    candidateVoucher.MaxDiscountAmount,
                    subTotalAmount);

                if (!VoucherLifecycleRules.CanRedeemBudget(candidateVoucher.BudgetLimit, candidateVoucher.BudgetUsed, voucherDiscount))
                {
                    return BadRequest(new { message = $"Ngân sách cho voucher '{candidateVoucher.Code}' đã hết." });
                }

                voucherToRedeem = candidateVoucher;
            }

            var totals = RetailOrderLifecycleRules.CalculateOrderTotals(subTotalAmount, taxAmountTotal, voucherDiscount, 0m);

            var retailOrder = new RetailOrder
            {
                BranchId = request.BranchId,
                OrderCode = GenerateRetailOrderCode(),
                CustomerId = customerId.Value,
                SubTotal = totals.SubTotal,
                TierDiscountAmount = 0m,
                VoucherId = voucherToRedeem?.Id,
                VoucherDiscountAmount = voucherDiscount,
                TotalDiscountAmount = totals.TotalDiscount,
                TaxAmount = totals.TaxAmount,
                FinalAmount = totals.FinalAmount,
                Status = nameof(RetailOrderStatus.DRAFT),
                CustomerNotes = string.IsNullOrWhiteSpace(request.CustomerNotes)
                    ? null
                    : request.CustomerNotes.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            var strategy = _db.Database.CreateExecutionStrategy();
            var result = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await TryBeginTransactionAsync();
                try
                {
                    _db.RetailOrders.Add(retailOrder);
                    await SaveChangesInTransactionAsync();

                    foreach (var item in retailOrderItems)
                    {
                        item.RetailOrderId = retailOrder.Id;
                    }

                    _db.RetailOrderItems.AddRange(retailOrderItems);
                    await SaveChangesInTransactionAsync();

                    if (voucherToRedeem != null)
                    {
                        var redemption = new VoucherRedemption
                        {
                            VoucherId = voucherToRedeem.Id,
                            VoucherCode = voucherToRedeem.Code,
                            CustomerId = customerId.Value,
                            BranchId = request.BranchId,
                            RetailOrderId = retailOrder.Id,
                            ValidationStatus = "VALIDATED",
                            RedemptionStatus = "REDEEMED",
                            EligibleAmount = totals.SubTotal,
                            DiscountAmount = voucherDiscount,
                            BudgetImpact = voucherDiscount,
                            AppliedBy = customerId.Value,
                            IdempotencyKey = $"ro-checkout-{retailOrder.OrderCode}",
                            AppliedAt = now,
                            RedeemedAt = now,
                            CreatedAt = now
                        };
                        _db.VoucherRedemptions.Add(redemption);

                        voucherToRedeem.UsedCount = (voucherToRedeem.UsedCount ?? 0) + 1;
                        voucherToRedeem.RedeemedCount += 1;
                        voucherToRedeem.BudgetUsed += voucherDiscount;
                        voucherToRedeem.UpdatedAt = now;
                        voucherToRedeem.UpdatedBy = customerId.Value;

                        var exhaustedByUsage = voucherToRedeem.UsageLimit.HasValue && voucherToRedeem.UsageLimit.Value > 0 && voucherToRedeem.UsedCount >= voucherToRedeem.UsageLimit.Value;
                        var exhaustedByBudget = voucherToRedeem.BudgetLimit.HasValue && voucherToRedeem.BudgetUsed >= voucherToRedeem.BudgetLimit.Value;
                        if (exhaustedByUsage || exhaustedByBudget)
                        {
                            voucherToRedeem.Status = VoucherLifecycleRules.StatusExhausted;
                            voucherToRedeem.IsActive = false;
                        }

                        await SaveChangesInTransactionAsync();
                    }

                    if (transaction != null) await transaction.CommitAsync();

                    return retailOrder;
                }
                catch
                {
                    if (transaction != null) await transaction.RollbackAsync();
                    throw;
                }
            });

            var responseDto = _mapper.Map<RetailOrderResponseDto>(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<RetailOrderResponseDto>> Create([FromBody] RetailOrderCreateDto itemDto)
        {
            return StatusCode(405, new
            {
                message = "Retail order must be created through the controlled checkout workflow."
            });
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id:long}")]
        public async Task<ActionResult<RetailOrderResponseDto>> Update(long id, [FromBody] RetailOrderUpdateDto itemDto)
        {
            return StatusCode(405, new
            {
                message = "Retail order must be updated through a controlled business workflow, not generic CRUD."
            });
        }

        [Authorize(Roles = "Staff")]
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _retailOrderService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
        {
            if (_db.Database.CurrentTransaction != null)
            {
                return null;
            }

            try
            {
                return await _db.Database.BeginTransactionAsync();
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

        private async Task SaveChangesInTransactionAsync()
        {
            await using var tx = await TryBeginTransactionAsync();
            await _db.SaveChangesAsync();
            if (tx != null)
            {
                await tx.CommitAsync();
            }
        }
    }
}




