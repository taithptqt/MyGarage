using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/vehicles")]
    [ApiController]
    [Authorize]
    public class VehiclesController : ControllerBase
    {
        private readonly IVehicleService _vehicleService;
        private readonly IVehicleHistoryService _vehicleHistoryService;
        private readonly IVehicleQueryService _vehicleQueryService;

        public VehiclesController(
            IVehicleService vehicleService,
            IVehicleHistoryService vehicleHistoryService,
            IVehicleQueryService vehicleQueryService)
        {
            _vehicleService = vehicleService;
            _vehicleHistoryService = vehicleHistoryService;
            _vehicleQueryService = vehicleQueryService;
        }

        [Authorize(Roles = "Staff")]
        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<VehicleResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _vehicleQueryService.GetPagedAsync(normalizedPage, normalizedSize);
            var dtos = _vehicleService.MapToDtos(paged);
            return Ok(PagedEnvelope<VehicleResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VehicleResponseDto>> GetById(long id)
        {
            var dto = await _vehicleService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            if (!await CanAccessVehicleAsync(id))
            {
                return Forbid();
            }

            return Ok(dto);
        }

        [HttpGet("{id:long}/service-history")]
        public async Task<ActionResult<VehicleServiceHistoryResponseDto>> GetServiceHistory(
            long id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (!await CanAccessVehicleAsync(id))
            {
                return Forbid();
            }

            var history = await _vehicleHistoryService.GetServiceHistoryAsync(id, page, pageSize);
            if (history == null)
            {
                return NotFound();
            }

            return Ok(history);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<VehicleResponseDto>> Create([FromBody] VehicleCreateDto vehicleDto)
        {
            var responseDto = await _vehicleService.CreateDtoAsync(vehicleDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id:long}")]
        public async Task<ActionResult<VehicleResponseDto>> Update(long id, [FromBody] VehicleUpdateDto vehicleDto)
        {
            var responseDto = await _vehicleService.UpdateDtoAsync(id, vehicleDto);
            if (responseDto == null)
            {
                return NotFound();
            }

            return Ok(responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _vehicleService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        private async Task<bool> CanAccessVehicleAsync(long vehicleId)
        {
            if (this.IsStaffUser())
            {
                return true;
            }

            var currentCustomerId = this.GetCurrentUserId();
            if (!currentCustomerId.HasValue)
            {
                return false;
            }

            return await _vehicleQueryService.CustomerOwnsVehicleAsync(vehicleId, currentCustomerId.Value);
        }
    }
}
