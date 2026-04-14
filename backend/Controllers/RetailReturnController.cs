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
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/retail-returns")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class RetailReturnController : ControllerBase
    {
        private readonly IRetailReturnService _retailReturnService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public RetailReturnController(IRetailReturnService retailReturnService, IGarageContext context, IMapper mapper)
        {
            _retailReturnService = retailReturnService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RetailReturnResponseDto>>> GetAll()
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<RetailReturn> query = _context.RetailReturns
                .AsNoTracking()
                .Include(item => item.RetailOrder);

            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(item => item.RetailOrder != null && item.RetailOrder.BranchId == branchId);
            }

            var items = await query.ToListAsync();
            var dtos = _mapper.Map<IEnumerable<RetailReturnResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> GetById(long id)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var item = await _context.RetailReturns
                .AsNoTracking()
                .Include(x => x.RetailOrder)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && item.RetailOrder?.BranchId != scopedBranchId.Value)
            {
                return NotFound();
            }

            var dto = _mapper.Map<RetailReturnResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailReturnResponseDto>> Create([FromBody] RetailReturnCreateDto itemDto)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            if (itemDto == null || itemDto.RetailOrderId <= 0)
            {
                return BadRequest(new { message = "RetailOrderId is required." });
            }

            var retailOrder = await _context.RetailOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == itemDto.RetailOrderId);
            if (retailOrder == null)
            {
                return BadRequest(new { message = "Retail order not found." });
            }

            if (scopedBranchId.HasValue && retailOrder.BranchId != scopedBranchId.Value)
            {
                return NotFound();
            }

            var now = VietnamTime.Now;
            var entity = _mapper.Map<RetailReturn>(itemDto);
            entity.Status = nameof(RetailReturnStatusCode.PENDING);
            entity.CreatedBy = actorUserId.Value;
            entity.UpdatedBy = actorUserId.Value;
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.ReceivedBy = actorUserId.Value;
            entity.ReturnDate = itemDto.ReturnDate ?? now;
            entity.ApprovedBy = null;
            entity.ApprovedAt = null;
            var createdItem = await _retailReturnService.AddAsync(entity);

            await WriteAuditAsync(
                actorUserId.Value,
                retailOrder.BranchId,
                "RETAIL_RETURN_CREATED",
                createdItem.Id,
                null,
                JsonSerializer.Serialize(new
                {
                    createdItem.RetailOrderId,
                    createdItem.TotalRefundAmount,
                    createdItem.Status,
                    createdItem.Reason
                }));

            var responseDto = _mapper.Map<RetailReturnResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPost("{id:long}/approve")]
        public async Task<ActionResult<RetailReturnResponseDto>> Approve(long id)
        {
            return await TransitionStatusAsync(id, nameof(RetailReturnStatusCode.APPROVED));
        }

        [HttpPost("{id:long}/reject")]
        public async Task<ActionResult<RetailReturnResponseDto>> Reject(long id)
        {
            return await TransitionStatusAsync(id, nameof(RetailReturnStatusCode.REJECTED));
        }

        [HttpPost("{id:long}/mark-refunded")]
        public async Task<ActionResult<RetailReturnResponseDto>> MarkRefunded(long id)
        {
            return await TransitionStatusAsync(id, nameof(RetailReturnStatusCode.COMPLETED));
        }

        [HttpPut("{id}")]
        public Task<ActionResult<RetailReturnResponseDto>> Update(long id, [FromBody] RetailReturnUpdateDto itemDto)
        {
            _ = id;
            _ = itemDto;
            return Task.FromResult<ActionResult<RetailReturnResponseDto>>(StatusCode(405, new
            {
                message = "Retail return is workflow-driven. Use /approve, /reject, /mark-refunded."
            }));
        }

        [HttpDelete("{id}")]
        public Task<IActionResult> Delete(long id)
        {
            _ = id;
            return Task.FromResult<IActionResult>(StatusCode(405, new
            {
                message = "Retail return cannot be deleted once created. Use workflow transitions instead."
            }));
        }

        private async Task<ActionResult<RetailReturnResponseDto>> TransitionStatusAsync(long id, string nextStatus)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var existingItem = await _context.RetailReturns
                .Include(x => x.RetailOrder)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && existingItem.RetailOrder?.BranchId != scopedBranchId.Value)
            {
                return NotFound();
            }

            var currentStatus = (existingItem.Status ?? string.Empty).Trim().ToUpperInvariant();
            var normalizedNextStatus = (nextStatus ?? string.Empty).Trim().ToUpperInvariant();
            var isValidTransition =
                (currentStatus == nameof(RetailReturnStatusCode.PENDING) && normalizedNextStatus is nameof(RetailReturnStatusCode.APPROVED) or nameof(RetailReturnStatusCode.REJECTED))
                || (currentStatus == nameof(RetailReturnStatusCode.APPROVED) && normalizedNextStatus == nameof(RetailReturnStatusCode.COMPLETED));

            if (!isValidTransition)
            {
                return BadRequest(new
                {
                    message = $"Invalid transition from {currentStatus} to {normalizedNextStatus}."
                });
            }

            var oldSnapshot = JsonSerializer.Serialize(new
            {
                existingItem.Status,
                existingItem.ApprovedBy,
                existingItem.ApprovedAt
            });

            var now = VietnamTime.Now;
            existingItem.Status = normalizedNextStatus;
            existingItem.UpdatedBy = actorUserId.Value;
            existingItem.UpdatedAt = now;

            if (normalizedNextStatus is nameof(RetailReturnStatusCode.APPROVED) or nameof(RetailReturnStatusCode.REJECTED))
            {
                existingItem.ApprovedBy = actorUserId.Value;
                existingItem.ApprovedAt = now;
            }

            var updatedItem = await _retailReturnService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            await WriteAuditAsync(
                actorUserId.Value,
                existingItem.RetailOrder?.BranchId,
                $"RETAIL_RETURN_{normalizedNextStatus}",
                existingItem.Id,
                oldSnapshot,
                JsonSerializer.Serialize(new
                {
                    updatedItem.Status,
                    updatedItem.ApprovedBy,
                    updatedItem.ApprovedAt,
                    updatedItem.UpdatedBy,
                    updatedItem.UpdatedAt
                }));

            var responseDto = _mapper.Map<RetailReturnResponseDto>(updatedItem);
            return Ok(responseDto);
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
                TableName = "RETAIL_RETURNS",
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
