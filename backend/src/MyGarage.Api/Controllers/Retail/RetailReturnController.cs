using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Common;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/v1/retail-returns")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class RetailReturnController : ControllerBase
    {
        private readonly IRetailReturnService _retailReturnService;
        private readonly IRetailReturnDataService _dataService;

        public RetailReturnController(IRetailReturnService retailReturnService, IRetailReturnDataService dataService)
        {
            _retailReturnService = retailReturnService;
            _dataService = dataService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<RetailReturnResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _dataService.GetPagedAsync(branchId, normalizedPage, normalizedSize);
            var dtos = _retailReturnService.MapToDtos(paged);
            return Ok(PagedEnvelope<RetailReturnResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var item = await _dataService.GetByIdAsync(id, branchId);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _retailReturnService.MapToDto(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailReturnResponseDto>> Create([FromBody] RetailReturnCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var actorId = this.GetCurrentUserId();
            if (!actorId.HasValue || actorId.Value <= 0) return Unauthorized();
            if (!await _dataService.RetailOrderExistsAsync(itemDto.RetailOrderId, branchId)) return NotFound();
            if (!string.IsNullOrWhiteSpace(itemDto.Status) && !string.Equals(itemDto.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                return this.BadRequestProblem("Phiếu trả hàng mới phải ở trạng thái chờ xử lý.");
            var responseDto = await _retailReturnService.CreatePendingDtoAsync(itemDto, actorId.Value);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> Update(long id, [FromBody] RetailReturnUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var existingItem = await _dataService.GetForUpdateAsync(id, branchId);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (!string.Equals(existingItem.Status, nameof(RetailReturnStatusCode.PENDING), StringComparison.OrdinalIgnoreCase))
                return this.BadRequestProblem("Chỉ có thể chỉnh sửa phiếu trả hàng đang chờ xử lý.");
            if (itemDto.RetailOrderId != existingItem.RetailOrderId ||
                (!string.IsNullOrWhiteSpace(itemDto.Status) && !string.Equals(itemDto.Status, existingItem.Status, StringComparison.OrdinalIgnoreCase)))
                return this.BadRequestProblem("Không thể thay đổi đơn hoặc trạng thái qua chức năng chỉnh sửa.");
            existingItem.Reason = itemDto.Reason;
            existingItem.ReturnDate = itemDto.ReturnDate;
            existingItem.UpdatedBy = this.GetCurrentUserId();
            var updatedItem = await _retailReturnService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _retailReturnService.MapToDto(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return denied!;
            var existing = await _dataService.GetByIdAsync(id, branchId);
            if (existing == null) return NotFound();
            if (!string.Equals(existing.Status, nameof(RetailReturnStatusCode.PENDING), StringComparison.OrdinalIgnoreCase))
                return this.BadRequestProblem("Chỉ có thể xóa phiếu trả hàng đang chờ xử lý.");
            var result = await _retailReturnService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPost("{id:long}/finalizations")]
        public async Task<IActionResult> FinalizeReturn(long id)
        {
            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0) return Unauthorized();
            if (!this.TryResolveBranchScope(null, out var scopedBranch, out var denied)) return denied!;
            var result = await _dataService.FinalizeReturnAsync(id, actorUserId.Value, scopedBranch);
            return result.Outcome switch
            {
                RetailReturnFinalizeStatus.NotFound => this.NotFoundProblem(result.Message),
                RetailReturnFinalizeStatus.BadRequest => this.BadRequestProblem(result.Message),
                RetailReturnFinalizeStatus.AlreadyCompleted => Ok(new { message = result.Message, retailReturnId = result.RetailReturnId }),
                _ => Ok(new { retailReturnId = result.RetailReturnId, status = result.Status, totalRefundAmount = result.TotalRefundAmount, message = result.Message })
            };
        }
    }
}



