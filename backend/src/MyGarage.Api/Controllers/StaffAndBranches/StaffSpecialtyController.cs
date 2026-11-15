using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Controllers
{
    [ApiController]
    [Authorize(Roles = "Staff")]
    public sealed class StaffSpecialtyController : ControllerBase
    {
        private readonly IStaffSpecialtyDataService _dataService;

        public StaffSpecialtyController(IStaffSpecialtyDataService dataService)
        {
            _dataService = dataService;
        }

        [Authorize]
        [HttpPost("api/v1/staff/{id:long}/specialties")]
        [HttpPost("api/v1/staffs/{id:long}/specialties")]
        public async Task<IActionResult> UpdateSpecialties(long id, [FromBody] StaffSpecialtyUpdateRequestDto? request)
        {
            var result = await _dataService.UpdateSpecialtiesAsync(
                id,
                request?.Specialties,
                this.GetCurrentUserId(),
                HttpContext.RequestAborted);
            if (result == null)
            {
                return this.NotFoundProblem("Không tìm thấy nhân viên.");
            }

            return Ok(new
            {
                staffId = result.StaffId,
                specialty = result.Specialty,
                specialties = result.Specialties
            });
        }

        [HttpGet("api/v1/staff/available")]
        [HttpGet("api/v1/staffs/available")]
        public async Task<ActionResult<IEnumerable<StaffAvailabilityResponseDto>>> GetAvailableStaff(
            [FromQuery] string? specialty,
            [FromQuery] DateTime? date,
            [FromQuery] long? branchId = null)
        {
            var response = await _dataService.GetAvailableStaffAsync(specialty, date, branchId, HttpContext.RequestAborted);
            return Ok(response);
        }
    }
}
