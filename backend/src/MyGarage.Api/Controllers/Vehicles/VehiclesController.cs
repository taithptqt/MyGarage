using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/vehicles")]
    [Route("api/v1/vehicles")]
    [ApiController]
    [Authorize]
    public class VehiclesController : ControllerBase
    {
        private readonly IVehicleService _vehicleService;
        private readonly IVehicleHistoryService _vehicleHistoryService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public VehiclesController(
            IVehicleService vehicleService,
            IVehicleHistoryService vehicleHistoryService,
            IGarageContext context,
            IMapper mapper)
        {
            _vehicleService = vehicleService;
            _vehicleHistoryService = vehicleHistoryService;
            _context = context;
            _mapper = mapper;
        }

        [Authorize(Roles = "Staff")]
        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<VehicleResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var query = _context.Vehicles.AsNoTracking()
                .Include(v => v.Model).ThenInclude(m => m.Brand)
                .Where(v => v.IsDeleted != true).OrderBy(v => v.Id);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var paged = offset >= total
                ? new List<Vehicle>()
                : await query.Skip((int)offset).Take(normalizedSize).ToListAsync();
            var dtos = _mapper.Map<List<VehicleResponseDto>>(paged);
            return Ok(PagedEnvelope<VehicleResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VehicleResponseDto>> GetById(long id)
        {
            var vehicle = await _vehicleService.GetByIdAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }

            if (!await CanAccessVehicleAsync(id))
            {
                return Forbid();
            }

            var dto = _mapper.Map<VehicleResponseDto>(vehicle);
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
            var entity = _mapper.Map<Vehicle>(vehicleDto);
            var createdVehicle = await _vehicleService.AddAsync(entity);
            var responseDto = _mapper.Map<VehicleResponseDto>(createdVehicle);
            return CreatedAtAction(nameof(GetById), new { id = createdVehicle.Id }, responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id:long}")]
        public async Task<ActionResult<VehicleResponseDto>> Update(long id, [FromBody] VehicleUpdateDto vehicleDto)
        {
            var existingVehicle = await _vehicleService.GetByIdAsync(id);
            if (existingVehicle == null)
            {
                return NotFound();
            }

            _mapper.Map(vehicleDto, existingVehicle);
            var updatedVehicle = await _vehicleService.UpdateAsync(id, existingVehicle);
            var responseDto = _mapper.Map<VehicleResponseDto>(updatedVehicle);
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

            return await _context.CustomerVehicles
                .AsNoTracking()
                .AnyAsync(cv => cv.VehicleId == vehicleId && cv.CustomerId == currentCustomerId.Value && cv.IsDeleted != true);
        }
    }
}
