using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [ApiController]
    [Route("api/technicians")]
    [Route("api/v1/technicians")]
    [Authorize]
    public sealed class TechniciansController : ControllerBase
    {
        private readonly ITechnicianProductivityService _technicianProductivityService;

        public TechniciansController(ITechnicianProductivityService technicianProductivityService)
        {
            _technicianProductivityService = technicianProductivityService;
        }

        [HttpGet("{id:long}/productivity")]
        public async Task<IActionResult> GetTechnicianProductivity(
            long id,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null)
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Technician id is invalid." });
            }

            var result = await _technicianProductivityService.GetTechnicianProductivityAsync(id, from, to);
            if (result == null)
            {
                return NotFound(new { message = "Technician not found." });
            }

            return Ok(result);
        }

        [HttpGet("productivity-report")]
        public async Task<IActionResult> GetProductivityReport(
            [FromQuery] string? period = null,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null,
            [FromQuery] long? branchId = null,
            [FromQuery] int top = 30)
        {
            var result = await _technicianProductivityService.GetProductivityReportAsync(period, from, to, branchId, top);
            return Ok(result);
        }
    }
}
