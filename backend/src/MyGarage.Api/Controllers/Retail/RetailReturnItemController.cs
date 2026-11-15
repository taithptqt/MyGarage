using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using System;
using System.Linq;

namespace MyGarage.Controllers
{
    [Route("api/retail-return-items")]
    [Route("api/v1/retail-return-items")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class RetailReturnItemController : ControllerBase
    {
        private readonly IRetailReturnItemService _retailReturnItemService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public RetailReturnItemController(IRetailReturnItemService retailReturnItemService, IMapper mapper, IGarageContext context)
        {
            _retailReturnItemService = retailReturnItemService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<RetailReturnItemResponseDto>>> GetAll([FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var query = _context.RetailReturnItems.AsNoTracking().AsQueryable();
            if (branchId.HasValue) query = query.Where(x => x.RetailReturn.RetailOrder.BranchId == branchId.Value);
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var items = offset >= total
                ? new List<RetailReturnItem>()
                : await query.OrderByDescending(x => x.Id).Skip((int)offset).Take(normalizedSize).ToListAsync();
            var dtos = _mapper.Map<List<RetailReturnItemResponseDto>>(items);
            return Ok(PagedEnvelope<RetailReturnItemResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailReturnItemResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var item = await _context.RetailReturnItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id &&
                (!branchId.HasValue || x.RetailReturn.RetailOrder.BranchId == branchId.Value));
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<RetailReturnItemResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailReturnItemResponseDto>> Create([FromBody] RetailReturnItemCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var actorId = this.GetCurrentUserId();
            if (!actorId.HasValue || actorId.Value <= 0) return Unauthorized();
            var parent = await _context.RetailReturns.AsNoTracking().Where(x => x.Id == itemDto.RetailReturnId &&
                (!branchId.HasValue || x.RetailOrder.BranchId == branchId.Value))
                .Select(x => new { x.Status, x.RetailOrderId, x.RetailOrder.BranchId }).FirstOrDefaultAsync();
            if (parent == null) return NotFound();
            if (!string.Equals(parent.Status, "PENDING", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(itemDto.Status) && !string.Equals(itemDto.Status, "PENDING", StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new { message = "Chỉ có thể thêm dòng vào phiếu trả hàng đang chờ xử lý." });
            if (!await _context.RetailOrderItems.AsNoTracking().AnyAsync(x => x.Id == itemDto.RetailOrderItemId && x.RetailOrderId == parent.RetailOrderId) ||
                !await _context.Warehouses.AsNoTracking().AnyAsync(x => x.Id == itemDto.WarehouseId && x.BranchId == parent.BranchId))
                return BadRequest(new { message = "Dòng đơn hàng hoặc kho không khớp với phiếu trả." });
            var entity = _mapper.Map<RetailReturnItem>(itemDto);
            entity.Status = "PENDING";
            entity.CreatedBy = actorId.Value;
            entity.UpdatedBy = null;
            var createdItem = await _retailReturnItemService.AddAsync(entity);
            var responseDto = _mapper.Map<RetailReturnItemResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailReturnItemResponseDto>> Update(long id, [FromBody] RetailReturnItemUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var existingItem = await _context.RetailReturnItems.Include(x => x.RetailReturn).ThenInclude(x => x.RetailOrder)
                .FirstOrDefaultAsync(x => x.Id == id && (!branchId.HasValue || x.RetailReturn.RetailOrder.BranchId == branchId.Value));
            if (existingItem == null)
            {
                return NotFound();
            }

            if (!string.Equals(existingItem.RetailReturn.Status, "PENDING", StringComparison.OrdinalIgnoreCase) ||
                itemDto.RetailReturnId != existingItem.RetailReturnId || itemDto.RetailOrderItemId != existingItem.RetailOrderItemId ||
                (!string.IsNullOrWhiteSpace(itemDto.Status) && !string.Equals(itemDto.Status, existingItem.Status, StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new { message = "Không thể thay đổi dòng hoặc trạng thái của phiếu trả hàng này." });
            if (!await _context.Warehouses.AsNoTracking().AnyAsync(x => x.Id == itemDto.WarehouseId && x.BranchId == existingItem.RetailReturn.RetailOrder.BranchId))
                return BadRequest(new { message = "Kho không thuộc chi nhánh của đơn bán lẻ." });
            existingItem.WarehouseId = itemDto.WarehouseId;
            if (itemDto.ReturnedQuantity.HasValue) existingItem.ReturnedQuantity = itemDto.ReturnedQuantity.Value;
            if (itemDto.RefundAmount.HasValue) existingItem.RefundAmount = itemDto.RefundAmount.Value;
            existingItem.ItemCondition = itemDto.ItemCondition ?? existingItem.ItemCondition;
            existingItem.Note = itemDto.Note;
            existingItem.ConditionEvidenceUrls = itemDto.ConditionEvidenceUrls;
            existingItem.UpdatedBy = this.GetCurrentUserId();
            var updatedItem = await _retailReturnItemService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<RetailReturnItemResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return denied!;
            var item = await _context.RetailReturnItems.AsNoTracking().Where(x => x.Id == id &&
                (!branchId.HasValue || x.RetailReturn.RetailOrder.BranchId == branchId.Value))
                .Select(x => new { x.RetailReturn.Status }).FirstOrDefaultAsync();
            if (item == null) return NotFound();
            if (!string.Equals(item.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Chỉ có thể xóa dòng của phiếu trả hàng đang chờ xử lý." });
            var result = await _retailReturnItemService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






