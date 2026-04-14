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
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/service-order-details")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ServiceOrderDetailController : ControllerBase
    {
        private readonly IServiceOrderDetailService _serviceOrderDetailService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public ServiceOrderDetailController(
            IServiceOrderDetailService serviceOrderDetailService,
            IServiceOrderService serviceOrderService,
            IGarageContext context,
            IMapper mapper)
        {
            _serviceOrderDetailService = serviceOrderDetailService;
            _serviceOrderService = serviceOrderService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderDetailResponseDto>>> GetAll([FromQuery] long? serviceOrderId = null)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            IEnumerable<ServiceOrderDetail> items;
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                if (scopedBranchId.HasValue)
                {
                    var inBranch = await IsServiceOrderInBranchAsync(serviceOrderId.Value, scopedBranchId.Value);
                    if (!inBranch) return NotFound();
                }

                items = await _context.ServiceOrderDetails
                    .AsNoTracking()
                    .Where(detail => detail.ServiceOrderId == serviceOrderId.Value)
                    .OrderBy(detail => detail.Id)
                    .ToListAsync();
            }
            else
            {
                IQueryable<ServiceOrderDetail> query = _context.ServiceOrderDetails.AsNoTracking();
                if (scopedBranchId.HasValue)
                {
                    var branchId = scopedBranchId.Value;
                    query = query.Where(detail =>
                        _context.ServiceOrders.Any(order =>
                            order.Id == detail.ServiceOrderId &&
                            (order.ExecuteBranchId == branchId || order.OriginBranchId == branchId)));
                }

                items = await query
                    .OrderBy(detail => detail.Id)
                    .ToListAsync();
            }

            var dtos = _mapper.Map<IEnumerable<ServiceOrderDetailResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> GetById(long id)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var item = await _serviceOrderDetailService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue)
            {
                var inBranch = await IsServiceOrderInBranchAsync(item.ServiceOrderId, scopedBranchId.Value);
                if (!inBranch) return NotFound();
            }

            var dto = _mapper.Map<ServiceOrderDetailResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> Create([FromBody] ServiceOrderDetailCreateDto itemDto)
        {
            try
            {
                if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

                if (itemDto == null || itemDto.ServiceOrderId <= 0)
                {
                    return BadRequest(new { message = "Thiếu ServiceOrderId hợp lệ để tạo hạng mục service order." });
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

                var entity = _mapper.Map<ServiceOrderDetail>(itemDto);
                ApplyCalculatedFinancialFields(
                    entity,
                    itemDto.Quantity ?? 0m,
                    itemDto.UnitPriceAtTime ?? 0m,
                    itemDto.DiscountAllocatedAmount ?? 0m,
                    itemDto.TaxRate ?? 0m);

                entity.Status = nameof(ServiceOrderDetailStatusCode.DRAFT);
                entity.AbsorbedApprovedBy = null;
                entity.CreatedBy = actorUserId.Value;
                entity.UpdatedBy = actorUserId.Value;
                entity.CreatedAt = VietnamTime.Now;
                entity.UpdatedAt = VietnamTime.Now;

                var createdItem = await _serviceOrderDetailService.AddAsync(entity);
                var responseDto = _mapper.Map<ServiceOrderDetailResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> Update(long id, [FromBody] ServiceOrderDetailUpdateDto itemDto)
        {
            try
            {
                if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

                var actorUserId = this.GetCurrentUserId();
                if (!actorUserId.HasValue || actorUserId.Value <= 0)
                {
                    return Unauthorized();
                }

                var existingItem = await _serviceOrderDetailService.GetByIdAsync(id);
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
                ApplyCalculatedFinancialFields(
                    existingItem,
                    itemDto?.Quantity ?? existingItem.Quantity,
                    itemDto?.UnitPriceAtTime ?? existingItem.UnitPriceAtTime,
                    itemDto?.DiscountAllocatedAmount ?? existingItem.DiscountAllocatedAmount ?? 0m,
                    itemDto?.TaxRate ?? existingItem.TaxRate);

                existingItem.CreatedBy = existingItem.CreatedBy > 0 ? existingItem.CreatedBy : actorUserId.Value;
                existingItem.UpdatedBy = actorUserId.Value;
                existingItem.UpdatedAt = VietnamTime.Now;

                var updatedItem = await _serviceOrderDetailService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<ServiceOrderDetailResponseDto>(updatedItem);
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

                var existingItem = await _serviceOrderDetailService.GetByIdAsync(id);
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

                var result = await _serviceOrderDetailService.DeleteAsync(id);
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

        private static void ApplyCalculatedFinancialFields(
            ServiceOrderDetail entity,
            decimal quantity,
            decimal unitPriceAtTime,
            decimal discountAllocatedAmount,
            decimal taxRate)
        {
            var normalizedQuantity = decimal.Round(Math.Max(0m, quantity), 2, MidpointRounding.AwayFromZero);
            var normalizedUnitPrice = decimal.Round(Math.Max(0m, unitPriceAtTime), 2, MidpointRounding.AwayFromZero);
            var normalizedDiscount = decimal.Round(Math.Max(0m, discountAllocatedAmount), 2, MidpointRounding.AwayFromZero);
            var normalizedTaxRate = decimal.Round(Math.Max(0m, taxRate), 2, MidpointRounding.AwayFromZero);

            var subTotal = decimal.Round(normalizedQuantity * normalizedUnitPrice, 2, MidpointRounding.AwayFromZero);
            var taxableBase = decimal.Round(Math.Max(0m, subTotal - normalizedDiscount), 2, MidpointRounding.AwayFromZero);
            var taxAmount = decimal.Round(taxableBase * normalizedTaxRate / 100m, 2, MidpointRounding.AwayFromZero);
            var finalAmount = decimal.Round(Math.Max(0m, taxableBase + taxAmount), 2, MidpointRounding.AwayFromZero);

            entity.Quantity = normalizedQuantity;
            entity.UnitPriceAtTime = normalizedUnitPrice;
            entity.DiscountAllocatedAmount = normalizedDiscount;
            entity.TaxRate = normalizedTaxRate;
            entity.SubTotal = subTotal;
            entity.TaxAmount = taxAmount;
            entity.FinalAmount = finalAmount;
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
