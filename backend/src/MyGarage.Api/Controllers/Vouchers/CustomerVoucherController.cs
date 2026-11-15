// Mục đích: Cung cấp API CRUD cho quan hệ gán voucher cho khách hàng và liên kết lịch sử sử dụng.
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Controllers
{
    [Route("api/v1/customer-vouchers")]
    [ApiController]
    [Authorize]
    public class CustomerVoucherController : ControllerBase
    {
        private readonly ICustomerVoucherService _customerVoucherService;
        private readonly ICustomerVoucherQueryService _customerVoucherQueryService;

        public CustomerVoucherController(
            ICustomerVoucherService customerVoucherService,
            ICustomerVoucherQueryService customerVoucherQueryService)
        {
            _customerVoucherService = customerVoucherService;
            _customerVoucherQueryService = customerVoucherQueryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<CustomerVoucherResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            long? customerId = null;
            if (!this.IsStaffUser())
            {
                var currentCustomerId = this.GetCurrentUserId();
                if (!currentCustomerId.HasValue)
                {
                    return Forbid();
                }

                customerId = currentCustomerId.Value;
            }

            var (items, total, normalizedPage, normalizedSize) = await _customerVoucherQueryService.GetPagedAsync(customerId, page, pageSize);
            var dtos = _customerVoucherService.MapToDtos(items);
            return Ok(PagedEnvelope<CustomerVoucherResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerVoucherResponseDto>> GetById(long id)
        {
            var dto = await _customerVoucherService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            if (!this.IsStaffUser() && dto.CustomerId != this.GetCurrentUserId())
            {
                return Forbid();
            }

            return Ok(dto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<CustomerVoucherResponseDto>> Create([FromBody] CustomerVoucherCreateDto itemDto)
        {
            var responseDto = await _customerVoucherService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerVoucherResponseDto>> Update(long id, [FromBody] CustomerVoucherUpdateDto itemDto)
        {
            var responseDto = await _customerVoucherService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _customerVoucherService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}









