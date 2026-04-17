using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/vehicles")]
    [ApiController]
    public class VehiclesController : ControllerBase
    {
        private readonly IVehicleService _vehicleService;
        private readonly IVehicleHistoryService _vehicleHistoryService;
        private readonly IMapper _mapper;

        public VehiclesController(
            IVehicleService vehicleService,
            IVehicleHistoryService vehicleHistoryService,
            IMapper mapper)
        {
            _vehicleService = vehicleService;
            _vehicleHistoryService = vehicleHistoryService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleResponseDto>>> GetAll()
        {
            var vehicles = await _vehicleService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<VehicleResponseDto>>(vehicles);
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VehicleResponseDto>> GetById(long id)
        {
            var vehicle = await _vehicleService.GetByIdAsync(id);
            if (vehicle == null)
            {
                return NotFound();
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
            var history = await _vehicleHistoryService.GetServiceHistoryAsync(id, page, pageSize);
            if (history == null)
            {
                return NotFound();
            }

            return Ok(history);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleResponseDto>> Create([FromBody] VehicleCreateDto vehicleDto)
        {
            var entity = _mapper.Map<Vehicle>(vehicleDto);
            var createdVehicle = await _vehicleService.AddAsync(entity);
            var responseDto = _mapper.Map<VehicleResponseDto>(createdVehicle);
            return CreatedAtAction(nameof(GetById), new { id = createdVehicle.Id }, responseDto);
        }

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
    }
}
