using System.Collections.Generic;
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
    [Route("api/v1/customer-feedbacks")]
    [ApiController]
    [Authorize]
    public class CustomerFeedbackController : ControllerBase
    {
        private readonly ICustomerFeedbackService _customerFeedbackService;
        private readonly ICustomerFeedbackQueryService _customerFeedbackQueryService;

        public CustomerFeedbackController(
            ICustomerFeedbackService customerFeedbackService,
            ICustomerFeedbackQueryService customerFeedbackQueryService)
        {
            _customerFeedbackService = customerFeedbackService;
            _customerFeedbackQueryService = customerFeedbackQueryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<CustomerFeedbackResponseDto>>> GetAll(
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

            var (items, total, normalizedPage, normalizedSize) = await _customerFeedbackQueryService.GetPagedAsync(customerId, page, pageSize);
            var dtos = _customerFeedbackService.MapToDtos(items);
            return Ok(PagedEnvelope<CustomerFeedbackResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerFeedbackResponseDto>> GetById(long id)
        {
            var dto = await _customerFeedbackService.GetDtoByIdAsync(id);
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

        [HttpPost]
        public async Task<ActionResult<CustomerFeedbackResponseDto>> Create([FromBody] CustomerFeedbackCreateDto itemDto)
        {
            long? overrideCustomerId = null;
            if (!this.IsStaffUser())
            {
                var currentCustomerId = this.GetCurrentUserId();
                if (!currentCustomerId.HasValue)
                {
                    return Forbid();
                }

                overrideCustomerId = currentCustomerId.Value;
            }

            var responseDto = await _customerFeedbackService.CreateDtoAsync(itemDto, overrideCustomerId);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerFeedbackResponseDto>> Update(long id, [FromBody] CustomerFeedbackUpdateDto itemDto)
        {
            var existingItem = await _customerFeedbackService.GetDtoByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var isStaff = this.IsStaffUser();
            if (!isStaff && existingItem.CustomerId != this.GetCurrentUserId())
            {
                return Forbid();
            }

            var responseDto = await _customerFeedbackService.UpdateDtoAsync(id, itemDto, preserveCustomerId: !isStaff);
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
            var result = await _customerFeedbackService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






