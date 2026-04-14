using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    [Route("api/retail-return-items")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class RetailReturnItemController : ControllerBase
    {
        private readonly IRetailReturnItemService _retailReturnItemService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public RetailReturnItemController(IRetailReturnItemService retailReturnItemService, IGarageContext context, IMapper mapper)
        {
            _retailReturnItemService = retailReturnItemService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RetailReturnItemResponseDto>>> GetAll()
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<RetailReturnItem> query = _context.RetailReturnItems
                .AsNoTracking()
                .Include(item => item.RetailReturn)
                    .ThenInclude(ret => ret.RetailOrder);

            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(item =>
                    item.RetailReturn != null &&
                    item.RetailReturn.RetailOrder != null &&
                    item.RetailReturn.RetailOrder.BranchId == branchId);
            }

            var items = await query.ToListAsync();
            var dtos = _mapper.Map<IEnumerable<RetailReturnItemResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailReturnItemResponseDto>> GetById(long id)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var item = await _context.RetailReturnItems
                .AsNoTracking()
                .Include(x => x.RetailReturn)
                    .ThenInclude(ret => ret.RetailOrder)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue &&
                item.RetailReturn?.RetailOrder?.BranchId != scopedBranchId.Value)
            {
                return NotFound();
            }

            var dto = _mapper.Map<RetailReturnItemResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailReturnItemResponseDto>> Create([FromBody] RetailReturnItemCreateDto itemDto)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            if (itemDto == null || itemDto.RetailReturnId <= 0 || itemDto.RetailOrderItemId <= 0)
            {
                return BadRequest(new { message = "RetailReturnId and RetailOrderItemId are required." });
            }

            var relatedData = await (
                from returnItem in _context.RetailReturns.AsNoTracking()
                join order in _context.RetailOrders.AsNoTracking() on returnItem.RetailOrderId equals order.Id
                join orderItem in _context.RetailOrderItems.AsNoTracking() on itemDto.RetailOrderItemId equals orderItem.Id
                where returnItem.Id == itemDto.RetailReturnId && orderItem.RetailOrderId == order.Id
                select new
                {
                    BranchId = order.BranchId,
                    OrderQuantity = orderItem.Quantity,
                    OrderLineFinalAmount = orderItem.FinalAmount
                }).FirstOrDefaultAsync();

            if (relatedData == null)
            {
                return BadRequest(new { message = "Invalid return/order-item relationship." });
            }

            if (scopedBranchId.HasValue && relatedData.BranchId != scopedBranchId.Value)
            {
                return NotFound();
            }

            var requestedQty = decimal.Round(Math.Max(0m, itemDto.ReturnedQuantity ?? 0m), 2, MidpointRounding.AwayFromZero);
            if (requestedQty <= 0m)
            {
                return BadRequest(new { message = "ReturnedQuantity must be greater than 0." });
            }

            if (requestedQty > relatedData.OrderQuantity)
            {
                return BadRequest(new { message = "ReturnedQuantity cannot exceed ordered quantity." });
            }

            var unitFinalAmount = relatedData.OrderQuantity <= 0m
                ? 0m
                : relatedData.OrderLineFinalAmount / relatedData.OrderQuantity;
            var computedRefundAmount = decimal.Round(Math.Max(0m, unitFinalAmount * requestedQty), 2, MidpointRounding.AwayFromZero);

            var entity = _mapper.Map<RetailReturnItem>(itemDto);
            entity.ReturnedQuantity = requestedQty;
            entity.RefundAmount = computedRefundAmount;
            entity.CreatedBy = actorUserId.Value;
            entity.UpdatedBy = actorUserId.Value;
            entity.CreatedAt = VietnamTime.Now;
            entity.UpdatedAt = VietnamTime.Now;
            entity.Status = "PENDING";

            var createdItem = await _retailReturnItemService.AddAsync(entity);

            await WriteAuditAsync(
                actorUserId.Value,
                relatedData.BranchId,
                "RETAIL_RETURN_ITEM_CREATED",
                createdItem.Id,
                null,
                JsonSerializer.Serialize(new
                {
                    createdItem.RetailReturnId,
                    createdItem.RetailOrderItemId,
                    createdItem.ReturnedQuantity,
                    createdItem.RefundAmount,
                    createdItem.Status
                }));

            var responseDto = _mapper.Map<RetailReturnItemResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailReturnItemResponseDto>> Update(long id, [FromBody] RetailReturnItemUpdateDto itemDto)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var existingItem = await _context.RetailReturnItems
                .Include(x => x.RetailReturn)
                    .ThenInclude(ret => ret.RetailOrder)
                .Include(x => x.RetailOrderItem)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var branchId = existingItem.RetailReturn?.RetailOrder?.BranchId;
            if (scopedBranchId.HasValue && branchId != scopedBranchId.Value)
            {
                return NotFound();
            }

            var targetOrderItemId = itemDto?.RetailOrderItemId > 0
                ? itemDto.RetailOrderItemId
                : existingItem.RetailOrderItemId;

            var orderItem = await _context.RetailOrderItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == targetOrderItemId);
            if (orderItem == null)
            {
                return BadRequest(new { message = "Retail order item not found." });
            }

            var requestedQty = decimal.Round(Math.Max(0m, itemDto?.ReturnedQuantity ?? existingItem.ReturnedQuantity), 2, MidpointRounding.AwayFromZero);
            if (requestedQty <= 0m || requestedQty > orderItem.Quantity)
            {
                return BadRequest(new { message = "ReturnedQuantity is invalid." });
            }

            var unitFinalAmount = orderItem.Quantity <= 0m ? 0m : orderItem.FinalAmount / orderItem.Quantity;
            var computedRefundAmount = decimal.Round(Math.Max(0m, unitFinalAmount * requestedQty), 2, MidpointRounding.AwayFromZero);

            var oldSnapshot = JsonSerializer.Serialize(new
            {
                existingItem.ReturnedQuantity,
                existingItem.RefundAmount,
                existingItem.Status
            });

            _mapper.Map(itemDto, existingItem);
            existingItem.ReturnedQuantity = requestedQty;
            existingItem.RefundAmount = computedRefundAmount;
            existingItem.CreatedBy = existingItem.CreatedBy > 0 ? existingItem.CreatedBy : actorUserId.Value;
            existingItem.UpdatedBy = actorUserId.Value;
            existingItem.UpdatedAt = VietnamTime.Now;
            existingItem.Status = "PENDING";

            var updatedItem = await _retailReturnItemService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            await WriteAuditAsync(
                actorUserId.Value,
                branchId,
                "RETAIL_RETURN_ITEM_UPDATED",
                updatedItem.Id,
                oldSnapshot,
                JsonSerializer.Serialize(new
                {
                    updatedItem.ReturnedQuantity,
                    updatedItem.RefundAmount,
                    updatedItem.Status,
                    updatedItem.UpdatedBy,
                    updatedItem.UpdatedAt
                }));

            var responseDto = _mapper.Map<RetailReturnItemResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var existingItem = await _context.RetailReturnItems
                .AsNoTracking()
                .Include(x => x.RetailReturn)
                    .ThenInclude(ret => ret.RetailOrder)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var branchId = existingItem.RetailReturn?.RetailOrder?.BranchId;
            if (scopedBranchId.HasValue && branchId != scopedBranchId.Value)
            {
                return NotFound();
            }

            var result = await _retailReturnItemService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            await WriteAuditAsync(
                actorUserId.Value,
                branchId,
                "RETAIL_RETURN_ITEM_DELETED",
                id,
                JsonSerializer.Serialize(new
                {
                    existingItem.RetailReturnId,
                    existingItem.RetailOrderItemId,
                    existingItem.ReturnedQuantity,
                    existingItem.RefundAmount
                }),
                null);

            return NoContent();
        }

        private async Task WriteAuditAsync(
            long actorUserId,
            long? branchId,
            string action,
            long recordId,
            string? oldValue,
            string? newValue)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                UserType = "STAFF",
                BranchId = branchId,
                Action = action,
                TableName = "RETAIL_RETURN_ITEMS",
                RecordId = recordId,
                OldValue = oldValue,
                NewValue = newValue,
                Timestamp = VietnamTime.Now
            });
            await _context.SaveChangesAsync();
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
    }
}
