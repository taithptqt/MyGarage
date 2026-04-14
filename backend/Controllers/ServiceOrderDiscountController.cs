using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/service-order-discounts")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ServiceOrderDiscountController : ControllerBase
    {
        private readonly IServiceOrderDiscountService _serviceOrderDiscountService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public ServiceOrderDiscountController(
            IServiceOrderDiscountService serviceOrderDiscountService,
            IServiceOrderService serviceOrderService,
            IGarageContext context,
            IMapper mapper)
        {
            _serviceOrderDiscountService = serviceOrderDiscountService;
            _serviceOrderService = serviceOrderService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderDiscountResponseDto>>> GetAll()
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<ServiceOrderDiscount> query = _context.ServiceOrderDiscounts.AsNoTracking();
            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(discount =>
                    _context.ServiceOrders.Any(order =>
                        order.Id == discount.ServiceOrderId &&
                        (order.ExecuteBranchId == branchId || order.OriginBranchId == branchId)));
            }

            var items = await query.ToListAsync();
            var dtos = _mapper.Map<IEnumerable<ServiceOrderDiscountResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderDiscountResponseDto>> GetById(long id)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var item = await _serviceOrderDiscountService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue)
            {
                var inBranch = await IsServiceOrderInBranchAsync(item.ServiceOrderId, scopedBranchId.Value);
                if (!inBranch) return NotFound();
            }

            var dto = _mapper.Map<ServiceOrderDiscountResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderDiscountResponseDto>> Create([FromBody] ServiceOrderDiscountCreateDto itemDto)
        {
            try
            {
                if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

                if (itemDto == null || itemDto.ServiceOrderId <= 0)
                {
                    return BadRequest(new { message = "Thiếu ServiceOrderId hợp lệ để tạo chiết khấu lệnh sửa chữa." });
                }

                var actorUserId = this.GetCurrentUserId();
                if (!actorUserId.HasValue || actorUserId.Value <= 0)
                {
                    return Unauthorized();
                }

                if (scopedBranchId.HasValue)
                {
                    var inBranch = await IsServiceOrderInBranchAsync(itemDto.ServiceOrderId, scopedBranchId.Value);
                    if (!inBranch) return NotFound();
                }

                await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(itemDto.ServiceOrderId);

                var entity = _mapper.Map<ServiceOrderDiscount>(itemDto);
                entity.AppliedBy = actorUserId.Value;
                entity.AppliedAmount = await CalculateAppliedAmountAsync(itemDto.ServiceOrderId, itemDto.DiscountValue ?? 0m, itemDto.ValueType);
                var createdItem = await _serviceOrderDiscountService.AddAsync(entity);
                var responseDto = _mapper.Map<ServiceOrderDiscountResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderDiscountResponseDto>> Update(long id, [FromBody] ServiceOrderDiscountUpdateDto itemDto)
        {
            try
            {
                if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

                var actorUserId = this.GetCurrentUserId();
                if (!actorUserId.HasValue || actorUserId.Value <= 0)
                {
                    return Unauthorized();
                }

                var existingItem = await _serviceOrderDiscountService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var targetServiceOrderId = itemDto?.ServiceOrderId > 0
                    ? itemDto.ServiceOrderId
                    : existingItem.ServiceOrderId;

                if (scopedBranchId.HasValue)
                {
                    var inBranch = await IsServiceOrderInBranchAsync(targetServiceOrderId, scopedBranchId.Value);
                    if (!inBranch) return NotFound();
                }

                await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(targetServiceOrderId);

                _mapper.Map(itemDto, existingItem);
                existingItem.AppliedBy = actorUserId.Value;
                existingItem.AppliedAmount = await CalculateAppliedAmountAsync(
                    targetServiceOrderId,
                    itemDto?.DiscountValue ?? existingItem.DiscountValue,
                    itemDto?.ValueType ?? existingItem.ValueType);

                var updatedItem = await _serviceOrderDiscountService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<ServiceOrderDiscountResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

                var existingItem = await _serviceOrderDiscountService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                if (scopedBranchId.HasValue)
                {
                    var inBranch = await IsServiceOrderInBranchAsync(existingItem.ServiceOrderId, scopedBranchId.Value);
                    if (!inBranch) return NotFound();
                }

                await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(existingItem.ServiceOrderId);

                var result = await _serviceOrderDiscountService.DeleteAsync(id);
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<decimal> CalculateAppliedAmountAsync(long serviceOrderId, decimal discountValue, string? valueType)
        {
            var order = await _context.ServiceOrders
                .AsNoTracking()
                .Where(item => item.Id == serviceOrderId)
                .Select(item => new
                {
                    item.SubTotalAmount,
                    item.FinalAmount
                })
                .FirstOrDefaultAsync();

            var baseAmount = order?.SubTotalAmount ?? order?.FinalAmount ?? 0m;
            var normalizedBase = decimal.Round(Math.Max(0m, baseAmount), 2, MidpointRounding.AwayFromZero);
            var normalizedDiscount = decimal.Round(Math.Max(0m, discountValue), 2, MidpointRounding.AwayFromZero);
            var normalizedValueType = (valueType ?? string.Empty).Trim().ToUpperInvariant();

            decimal appliedAmount;
            if (normalizedValueType is "PERCENT" or "PERCENTAGE")
            {
                appliedAmount = normalizedBase * normalizedDiscount / 100m;
            }
            else
            {
                appliedAmount = normalizedDiscount;
            }

            return decimal.Round(Math.Clamp(appliedAmount, 0m, normalizedBase), 2, MidpointRounding.AwayFromZero);
        }

        private bool TryResolveFinanceBranchScope(out long? branchId, out ActionResult deniedResult)
        {
            branchId = null;
            deniedResult = null!;

            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                deniedResult = Unauthorized();
                return false;
            }

            if (this.RequiresFinanceBranchScope())
            {
                branchId = this.GetCurrentBranchId();
                if (!branchId.HasValue)
                {
                    deniedResult = Forbid();
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> IsServiceOrderInBranchAsync(long serviceOrderId, long branchId)
        {
            return await _context.ServiceOrders
                .AsNoTracking()
                .AnyAsync(order =>
                    order.Id == serviceOrderId &&
                    (order.ExecuteBranchId == branchId || order.OriginBranchId == branchId));
        }
    }
}
