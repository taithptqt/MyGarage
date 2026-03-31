using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using MyGarage.Models;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Use VehiclesController with route /api/vehicles.")]
    public class VehicleController : ControllerBase
    {
        private readonly IVehicleService _vehicleService;
        private readonly IMapper _mapper;

        public VehicleController(IVehicleService vehicleService, IMapper mapper)
        {
            _vehicleService = vehicleService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleResponseDto>>> GetAll()
        {
            var vehicles = await _vehicleService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<VehicleResponseDto>>(vehicles);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
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

        [HttpPost]
        public async Task<ActionResult<VehicleResponseDto>> Create([FromBody] VehicleCreateDto vehicleDto)
        {
            var entity = _mapper.Map<Vehicle>(vehicleDto);
            var createdVehicle = await _vehicleService.AddAsync(entity);
            var responseDto = _mapper.Map<VehicleResponseDto>(createdVehicle);
            return CreatedAtAction(nameof(GetById), new { id = createdVehicle.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<VehicleResponseDto>> Update(long id, [FromBody] VehicleUpdateDto vehicleDto)
        {
            var existingVehicle = await _vehicleService.GetByIdAsync(id);
            if (existingVehicle == null) return NotFound();

            _mapper.Map(vehicleDto, existingVehicle);
            var updatedVehicle = await _vehicleService.UpdateAsync(id, existingVehicle);
            
            var responseDto = _mapper.Map<VehicleResponseDto>(updatedVehicle);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
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


