// Mục đích: Cung cấp API CRUD cho quan hệ gán voucher cho khách hàng và liên kết lịch sử sử dụng.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/customer-vouchers")]
    [Route("api/v1/customer-vouchers")]
    [ApiController]
    [Authorize]
    public class CustomerVoucherController : ControllerBase
    {
        private readonly ICustomerVoucherService _customerVoucherService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public CustomerVoucherController(ICustomerVoucherService customerVoucherService, IMapper mapper, IGarageContext context)
        {
            _customerVoucherService = customerVoucherService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<CustomerVoucherResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var query = _context.CustomerVouchers.AsNoTracking().AsQueryable();
            if (!this.IsStaffUser())
            {
                var currentCustomerId = this.GetCurrentUserId();
                if (!currentCustomerId.HasValue)
                {
                    return Forbid();
                }

                query = query.Where(x => x.CustomerId == currentCustomerId.Value);
            }

            var envelope = await query
                .OrderBy(x => x.Id)
                .ToPagedEnvelopeAsync(page, pageSize, paged => _mapper.Map<IEnumerable<CustomerVoucherResponseDto>>(paged));
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerVoucherResponseDto>> GetById(long id)
        {
            var item = await _customerVoucherService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!this.IsStaffUser() && item.CustomerId != this.GetCurrentUserId())
            {
                return Forbid();
            }

            var dto = _mapper.Map<CustomerVoucherResponseDto>(item);
            return Ok(dto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<CustomerVoucherResponseDto>> Create([FromBody] CustomerVoucherCreateDto itemDto)
        {
            var entity = _mapper.Map<CustomerVoucher>(itemDto);
            var createdItem = await _customerVoucherService.AddAsync(entity);
            var responseDto = _mapper.Map<CustomerVoucherResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerVoucherResponseDto>> Update(long id, [FromBody] CustomerVoucherUpdateDto itemDto)
        {
            var existingItem = await _customerVoucherService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _customerVoucherService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<CustomerVoucherResponseDto>(updatedItem);
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









