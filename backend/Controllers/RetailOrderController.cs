using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Controllers
{
    [Route("api/retail-orders")]
    [ApiController]
    public class RetailOrderController : ControllerBase
    {
        private readonly IRetailOrderService _retailOrderService;
        private readonly IVoucherRedemptionService _voucherRedemptionService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _db;

        public RetailOrderController(
            IRetailOrderService retailOrderService,
            IVoucherRedemptionService voucherRedemptionService,
            IMapper mapper,
            IGarageContext db)
        {
            _retailOrderService = retailOrderService;
            _voucherRedemptionService = voucherRedemptionService;
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
        public async Task<ActionResult<IEnumerable<RetailOrderResponseDto>>> GetAll()
        {
            var items = await _retailOrderService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<RetailOrderResponseDto>>(items);
            return Ok(dtos);
        }

        [Authorize]
        [HttpGet("customers/current")]
        public async Task<ActionResult<IEnumerable<RetailOrderResponseDto>>> GetMine()
        {
            var customerId = this.GetCurrentUserId();
            if (customerId == null)
            {
                return Unauthorized();
            }

            var items = await _db.RetailOrders
                .AsNoTracking()
                .Where(order => order.CustomerId == customerId.Value)
                .OrderByDescending(order => order.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(order => order.Id)
                .ToListAsync();

            var dtos = _mapper.Map<IEnumerable<RetailOrderResponseDto>>(items);
            return Ok(dtos);
        }

        [Authorize(Roles = "Staff")]
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
            var finalAmountBeforeDiscount = RoundCurrency(retailOrderItems.Sum(item => item.FinalAmount));

            var voucherRequested = request.VoucherId.HasValue || !string.IsNullOrWhiteSpace(request.VoucherCode);
            VoucherValidationResultResponseDto? voucherValidationResult = null;
            Voucher? validatedVoucher = null;
            var voucherDiscountAmount = 0m;

            if (voucherRequested)
            {
                var validationRequest = new VoucherRedemptionRequestDto
                {
                    VoucherId = request.VoucherId,
                    VoucherCode = request.VoucherCode,
                    CustomerId = customerId.Value,
                    BranchId = request.BranchId,
                    PaymentMethodId = request.PaymentMethodId,
                    RetailOrderId = null,
                    EligibleAmount = finalAmountBeforeDiscount,
                    Channel = string.IsNullOrWhiteSpace(request.Channel)
                        ? "RETAIL_CHECKOUT"
                        : request.Channel!.Trim().ToUpperInvariant(),
                    AppliedBy = customerId.Value
                };

                voucherValidationResult = await _voucherRedemptionService.CheckEligibilityAsync(validationRequest);
                if (!voucherValidationResult.IsEligible)
                {
                    return BadRequest(voucherValidationResult);
                }

                voucherDiscountAmount = RoundCurrency(voucherValidationResult.DiscountPreview ?? 0m);
                voucherDiscountAmount = Math.Min(voucherDiscountAmount, finalAmountBeforeDiscount);
                if (voucherDiscountAmount <= 0m)
                {
                    return BadRequest(new
                    {
                        message = "Voucher discount amount is invalid.",
                        reasonCode = "VCH_DISCOUNT_INVALID"
                    });
                }

                if (!voucherValidationResult.VoucherId.HasValue || voucherValidationResult.VoucherId.Value <= 0)
                {
                    return BadRequest(new
                    {
                        message = "Voucher validation did not return a valid voucher id.",
                        reasonCode = "VCH_NOT_FOUND"
                    });
                }

                validatedVoucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.Id == voucherValidationResult.VoucherId.Value);
                if (validatedVoucher == null)
                {
                    return BadRequest(new
                    {
                        message = "Validated voucher is no longer available.",
                        reasonCode = "VCH_NOT_FOUND"
                    });
                }
            }

            var totalDiscountAmount = RoundCurrency(voucherDiscountAmount);
            var finalAmountTotal = RoundCurrency(Math.Max(0m, finalAmountBeforeDiscount - totalDiscountAmount));
            var now = DateTime.UtcNow;

            var retailOrder = new RetailOrder
            {
                BranchId = request.BranchId,
                OrderCode = GenerateRetailOrderCode(),
                CustomerId = customerId.Value,
                SubTotal = subTotalAmount,
                TierDiscountAmount = 0m,
                VoucherId = voucherValidationResult?.VoucherId,
                VoucherDiscountAmount = voucherDiscountAmount > 0m ? voucherDiscountAmount : 0m,
                TotalDiscountAmount = totalDiscountAmount,
                TaxAmount = taxAmountTotal,
                FinalAmount = finalAmountTotal,
                Status = nameof(RetailOrderStatus.DRAFT),
                CustomerNotes = string.IsNullOrWhiteSpace(request.CustomerNotes)
                    ? null
                    : request.CustomerNotes.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            await using var transaction = await _db.Database.BeginTransactionAsync();

            _db.RetailOrders.Add(retailOrder);
            await _db.SaveChangesAsync();

            foreach (var item in retailOrderItems)
            {
                item.RetailOrderId = retailOrder.Id;
            }

            _db.RetailOrderItems.AddRange(retailOrderItems);

            if (validatedVoucher != null && voucherValidationResult != null && voucherDiscountAmount > 0m)
            {
                var redemptionTimestamp = VietnamTime.Now;
                _db.VoucherRedemptions.Add(new VoucherRedemption
                {
                    VoucherId = validatedVoucher.Id,
                    VoucherCode = validatedVoucher.Code,
                    CustomerId = customerId.Value,
                    BranchId = request.BranchId,
                    RetailOrderId = retailOrder.Id,
                    PaymentMethodId = request.PaymentMethodId,
                    ValidationStatus = nameof(VoucherRedemptionValidationStatusCode.VALIDATED),
                    RedemptionStatus = nameof(VoucherRedemptionStatusCode.REDEEMED),
                    EligibleAmount = finalAmountBeforeDiscount,
                    DiscountAmount = voucherDiscountAmount,
                    BudgetImpact = voucherDiscountAmount,
                    AppliedBy = customerId.Value,
                    IdempotencyKey = $"retail-checkout-{retailOrder.Id}-{validatedVoucher.Id}",
                    AppliedAt = redemptionTimestamp,
                    RedeemedAt = redemptionTimestamp,
                    CreatedAt = redemptionTimestamp
                });

                validatedVoucher.UsedCount = (validatedVoucher.UsedCount ?? 0) + 1;
                validatedVoucher.RedeemedCount += 1;
                validatedVoucher.BudgetUsed += voucherDiscountAmount;
                validatedVoucher.UpdatedAt = redemptionTimestamp;
                validatedVoucher.UpdatedBy = customerId.Value;

                var exhaustedByUsage = validatedVoucher.UsageLimit.HasValue
                    && validatedVoucher.UsageLimit.Value > 0
                    && validatedVoucher.UsedCount >= validatedVoucher.UsageLimit.Value;
                var exhaustedByBudget = validatedVoucher.BudgetLimit.HasValue
                    && validatedVoucher.BudgetUsed >= validatedVoucher.BudgetLimit.Value;
                if (exhaustedByUsage || exhaustedByBudget)
                {
                    validatedVoucher.Status = nameof(VoucherStatusCode.EXHAUSTED);
                    validatedVoucher.IsActive = false;
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            var responseDto = _mapper.Map<RetailOrderResponseDto>(retailOrder);
            return CreatedAtAction(nameof(GetById), new { id = retailOrder.Id }, responseDto);
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
    }
}






