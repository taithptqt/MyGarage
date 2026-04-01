using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/booking-groups")]
    [ApiController]
    [Authorize]
    public class BookingGroupsController : ControllerBase
    {
        private readonly IBookingGroupService _bookingGroupService;
        private readonly IAppointmentService _appointmentService;

        public BookingGroupsController(IBookingGroupService bookingGroupService, IAppointmentService appointmentService)
        {
            _bookingGroupService = bookingGroupService;
            _appointmentService = appointmentService;
        }

        [HttpPost]
        public async Task<ActionResult<BookingGroupResponseDto>> Create([FromBody] BookAppointmentBatchRequestDto dto)
        {
            var customerId = this.GetCurrentUserId();
            if (!customerId.HasValue)
            {
                return Unauthorized();
            }

            try
            {
                var result = await _appointmentService.BookBatchAsync(customerId.Value, dto);
                return CreatedAtAction(nameof(GetMyGroupDetail), new { id = result.Id }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("customers/current")]
        public async Task<ActionResult<IEnumerable<BookingGroupResponseDto>>> GetMyGroups()
        {
            var customerId = this.GetCurrentUserId();
            if (!customerId.HasValue)
            {
                return Unauthorized();
            }

            var groups = await _bookingGroupService.GetByCustomerAsync(customerId.Value);
            return Ok(groups);
        }

        [HttpGet("customers/current/{id:long}")]
        public async Task<ActionResult<BookingGroupResponseDto>> GetMyGroupDetail(long id)
        {
            var customerId = this.GetCurrentUserId();
            if (!customerId.HasValue)
            {
                return Unauthorized();
            }

            var group = await _bookingGroupService.GetByCustomerAndIdAsync(customerId.Value, id);
            if (group == null)
            {
                return NotFound();
            }

            return Ok(group);
        }
    }
}
