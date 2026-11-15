using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using System;

namespace MyGarage.Controllers
{
    [Route("api/v1/retail-return-items")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class RetailReturnItemController : ControllerBase
    {
        private readonly IRetailReturnItemService _retailReturnItemService;
        private readonly IRetailReturnItemQueryService _queryService;

        public RetailReturnItemController(IRetailReturnItemService retailReturnItemService, IRetailReturnItemQueryService queryService)
        {
            _retailReturnItemService = retailReturnItemService;
            _queryService = queryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<RetailReturnItemResponseDto>>> GetAll([FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (items, total) = await _queryService.GetPageAsync(branchId, normalizedPage, normalizedSize, HttpContext.RequestAborted);
            var dtos = _retailReturnItemService.MapToDtos(items);
            return Ok(PagedEnvelope<RetailReturnItemResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailReturnItemResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var item = await _queryService.GetByIdScopedAsync(id, branchId, HttpContext.RequestAborted);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _retailReturnItemService.MapToDto(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailReturnItemResponseDto>> Create([FromBody] RetailReturnItemCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var actorId = this.GetCurrentUserId();
            if (!actorId.HasValue || actorId.Value <= 0) return Unauthorized();
            var parent = await _queryService.GetParentForCreateAsync(itemDto.RetailReturnId, branchId, HttpContext.RequestAborted);
            if (parent == null) return NotFound();
            if (!string.Equals(parent.Status, "PENDING", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(itemDto.Status) && !string.Equals(itemDto.Status, "PENDING", StringComparison.OrdinalIgnoreCase)))
                return this.BadRequestProblem("Chỉ có thể thêm dòng vào phiếu trả hàng đang chờ xử lý.");
            if (!await _queryService.ValidateOrderItemAndWarehouseAsync(itemDto.RetailOrderItemId, parent.RetailOrderId, itemDto.WarehouseId, parent.BranchId, HttpContext.RequestAborted))
                return this.BadRequestProblem("Dòng đơn hàng hoặc kho không khớp với phiếu trả.");
            var responseDto = await _retailReturnItemService.CreatePendingDtoAsync(itemDto, actorId.Value);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailReturnItemResponseDto>> Update(long id, [FromBody] RetailReturnItemUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var existingItem = await _queryService.GetForUpdateScopedAsync(id, branchId, HttpContext.RequestAborted);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (!string.Equals(existingItem.RetailReturn.Status, "PENDING", StringComparison.OrdinalIgnoreCase) ||
                itemDto.RetailReturnId != existingItem.RetailReturnId || itemDto.RetailOrderItemId != existingItem.RetailOrderItemId ||
                (!string.IsNullOrWhiteSpace(itemDto.Status) && !string.Equals(itemDto.Status, existingItem.Status, StringComparison.OrdinalIgnoreCase)))
                return this.BadRequestProblem("Không thể thay đổi dòng hoặc trạng thái của phiếu trả hàng này.");
            if (!await _queryService.WarehouseBelongsToBranchAsync(itemDto.WarehouseId, existingItem.RetailReturn.RetailOrder.BranchId, HttpContext.RequestAborted))
                return this.BadRequestProblem("Kho không thuộc chi nhánh của đơn bán lẻ.");
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
            var responseDto = _retailReturnItemService.MapToDto(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return denied!;
            var status = await _queryService.GetParentStatusScopedAsync(id, branchId, HttpContext.RequestAborted);
            if (status == null) return NotFound();
            if (!string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase))
                return this.BadRequestProblem("Chỉ có thể xóa dòng của phiếu trả hàng đang chờ xử lý.");
            var result = await _retailReturnItemService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}
