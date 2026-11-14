using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/customer-vehicles")]
    [Route("api/v1/customer-vehicles")]
    [ApiController]
    [Authorize]
    public class CustomerVehiclesController : ControllerBase
    {
        private readonly ICustomerVehicleService _customerVehicleService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public CustomerVehiclesController(ICustomerVehicleService customerVehicleService, IMapper mapper, IGarageContext context)
        {
            _customerVehicleService = customerVehicleService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<CustomerVehicleResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var query = _context.CustomerVehicles.AsNoTracking().AsQueryable();
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
                .ToPagedEnvelopeAsync(page, pageSize, paged => _mapper.Map<IEnumerable<CustomerVehicleResponseDto>>(paged));
            return Ok(envelope);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<CustomerVehicleResponseDto>> GetById(long id)
        {
            var item = await _customerVehicleService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!this.IsStaffUser() && item.CustomerId != this.GetCurrentUserId())
            {
                return Forbid();
            }

            var dto = _mapper.Map<CustomerVehicleResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CustomerVehicleResponseDto>> Create([FromBody] CustomerVehicleCreateDto itemDto)
        {
            var entity = _mapper.Map<CustomerVehicle>(itemDto);
            if (!this.IsStaffUser())
            {
                var currentCustomerId = this.GetCurrentUserId();
                if (!currentCustomerId.HasValue)
                {
                    return Forbid();
                }

                entity.CustomerId = currentCustomerId.Value;
            }

            var createdItem = await _customerVehicleService.AddAsync(entity);
            var responseDto = _mapper.Map<CustomerVehicleResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<CustomerVehicleResponseDto>> Update(long id, [FromBody] CustomerVehicleUpdateDto itemDto)
        {
            var existingItem = await _customerVehicleService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (!this.IsStaffUser() && existingItem.CustomerId != this.GetCurrentUserId())
            {
                return Forbid();
            }

            var preservedCustomerId = existingItem.CustomerId;
            _mapper.Map(itemDto, existingItem);
            if (!this.IsStaffUser())
            {
                existingItem.CustomerId = preservedCustomerId;
            }

            var updatedItem = await _customerVehicleService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<CustomerVehicleResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var existingItem = await _customerVehicleService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (!this.IsStaffUser() && existingItem.CustomerId != this.GetCurrentUserId())
            {
                return Forbid();
            }

            var result = await _customerVehicleService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
