using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Exceptions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [ApiController]
    [Route("api/timesheets")]
    [Authorize]
    public sealed class TimesheetsController : ControllerBase
    {
        private readonly ITimesheetService _timesheetService;

        public TimesheetsController(ITimesheetService timesheetService)
        {
            _timesheetService = timesheetService;
        }

        [HttpPost("check-in")]
        public async Task<ActionResult<TimesheetEntryDto>> CheckIn([FromBody] TimesheetCheckInRequestDto request)
        {
            try
            {
                var data = await _timesheetService.CheckInAsync(request);
                return Ok(data);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:long}/check-out")]
        public async Task<ActionResult<TimesheetEntryDto>> CheckOut(long id, [FromBody] TimesheetCheckOutRequestDto request)
        {
            try
            {
                var data = await _timesheetService.CheckOutAsync(id, request);
                return Ok(data);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
