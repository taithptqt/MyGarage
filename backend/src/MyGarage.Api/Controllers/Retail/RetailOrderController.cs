using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/retail-orders")]
    [ApiController]
    public class RetailOrderController : ControllerBase
    {
        private readonly IRetailOrderService _retailOrderService;
        private readonly IRetailOrderDataService _dataService;

        public RetailOrderController(
            IRetailOrderService retailOrderService,
            IRetailOrderDataService dataService)
        {
            _retailOrderService = retailOrderService;
            _dataService = dataService;
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

            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _dataService.GetPagedByBranchAsync(
                scopedBranchId,
                normalizedPage,
                normalizedSize,
                HttpContext.RequestAborted);
            var dtos = _retailOrderService.MapToDtos(paged);
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
            var (paged, total) = await _dataService.GetPagedByCustomerAsync(
                customerId.Value,
                normalizedPage,
                normalizedSize,
                HttpContext.RequestAborted);
            var dtos = _retailOrderService.MapToDtos(paged);
            return Ok(PagedEnvelope<RetailOrderResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("customers/current/page")]
        public async Task<IActionResult> GetMinePage([FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] long? anchorId)
        {
            var customerId = this.GetCurrentUserId();
            if (!customerId.HasValue) return Unauthorized();
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize, 20, 100);
            var result = await _dataService.GetMinePageAsync(
                customerId.Value,
                normalizedPage,
                normalizedSize,
                anchorId,
                HttpContext.RequestAborted);
            return Ok(new
            {
                items = _retailOrderService.MapToDtos(result.Items),
                page = result.Page,
                pageSize = result.PageSize,
                total = result.Total,
                draftCount = result.DraftCount,
                confirmedCount = result.ConfirmedCount,
                totalValue = result.TotalValue
            });
        }

        [Authorize]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<RetailOrderResponseDto>> GetById(long id)
        {
            var item = await _dataService.GetWithItemsByIdAsync(id, HttpContext.RequestAborted);

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

            var dto = _retailOrderService.MapToDtoWithItems(item);

            return Ok(dto);
        }

        [Authorize]
        [HttpPost("checkouts")]
        public async Task<ActionResult<RetailOrderResponseDto>> Checkout([FromBody] RetailCheckoutRequestDto request)
        {
            if (request == null)
            {
                return this.BadRequestProblem("Retail checkout data is required.");
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
                return this.BadRequestProblem("BranchId must be greater than 0.");
            }

            var normalizedItems = (request.Items ?? new List<RetailCheckoutItemRequestDto>())
                .Where(item => item.ProductId > 0 && item.Quantity > 0)
                .GroupBy(item => item.ProductId)
                .Select(group => (ProductId: group.Key, Quantity: group.Sum(item => item.Quantity)))
                .ToList();

            if (normalizedItems.Count == 0)
            {
                return this.BadRequestProblem("Retail order must contain at least one item.");
            }

            var checkout = await _dataService.CheckoutAsync(
                customerId.Value,
                request.BranchId,
                normalizedItems,
                request.VoucherCode,
                request.CustomerNotes,
                HttpContext.RequestAborted);

            if (!checkout.Success)
            {
                return checkout.ErrorPayload != null
                    ? BadRequest(checkout.ErrorPayload)
                    : this.BadRequestProblem(checkout.ErrorMessage ?? "Không thể tạo đơn hàng.");
            }

            var responseDto = _retailOrderService.MapToDto(checkout.Order!);
            return CreatedAtAction(nameof(GetById), new { id = checkout.Order!.Id }, responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<RetailOrderResponseDto>> Create([FromBody] RetailOrderCreateDto itemDto)
        {
            return this.ProblemMessage(405, "Retail order must be created through the controlled checkout workflow.");
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id:long}")]
        public async Task<ActionResult<RetailOrderResponseDto>> Update(long id, [FromBody] RetailOrderUpdateDto itemDto)
        {
            return this.ProblemMessage(405, "Retail order must be updated through a controlled business workflow, not generic CRUD.");
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




