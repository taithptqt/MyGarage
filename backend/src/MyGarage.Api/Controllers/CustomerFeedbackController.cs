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
    [Route("api/customer-feedbacks")]
    [Route("api/v1/customer-feedbacks")]
    [ApiController]
    [Authorize]
    public class CustomerFeedbackController : ControllerBase
    {
        private readonly ICustomerFeedbackService _customerFeedbackService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public CustomerFeedbackController(ICustomerFeedbackService customerFeedbackService, IMapper mapper, IGarageContext context)
        {
            _customerFeedbackService = customerFeedbackService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<CustomerFeedbackResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var query = _context.CustomerFeedbacks.AsNoTracking().AsQueryable();
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
                .ToPagedEnvelopeAsync(page, pageSize, paged => _mapper.Map<IEnumerable<CustomerFeedbackResponseDto>>(paged));
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerFeedbackResponseDto>> GetById(long id)
        {
            var item = await _customerFeedbackService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!this.IsStaffUser() && item.CustomerId != this.GetCurrentUserId())
            {
                return Forbid();
            }

            var dto = _mapper.Map<CustomerFeedbackResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CustomerFeedbackResponseDto>> Create([FromBody] CustomerFeedbackCreateDto itemDto)
        {
            var entity = _mapper.Map<CustomerFeedback>(itemDto);
            if (!this.IsStaffUser())
            {
                var currentCustomerId = this.GetCurrentUserId();
                if (!currentCustomerId.HasValue)
                {
                    return Forbid();
                }

                entity.CustomerId = currentCustomerId.Value;
            }

            var createdItem = await _customerFeedbackService.AddAsync(entity);
            var responseDto = _mapper.Map<CustomerFeedbackResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerFeedbackResponseDto>> Update(long id, [FromBody] CustomerFeedbackUpdateDto itemDto)
        {
            var existingItem = await _customerFeedbackService.GetByIdAsync(id);
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

            var updatedItem = await _customerFeedbackService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<CustomerFeedbackResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _customerFeedbackService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






