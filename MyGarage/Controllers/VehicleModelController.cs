using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Use VehicleModelsController with route /api/vehicle-models.")]
    public class VehicleModelController : ControllerBase
    {
        private readonly IVehicleModelService _vehicleModelService;
        private readonly IMapper _mapper;

        public VehicleModelController(IVehicleModelService vehicleModelService, IMapper mapper)
        {
            _vehicleModelService = vehicleModelService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleModelResponseDto>>> GetAll()
        {
            var items = await _vehicleModelService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<VehicleModelResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VehicleModelResponseDto>> GetById(long id)
        {
            var item = await _vehicleModelService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<VehicleModelResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleModelResponseDto>> Create([FromBody] VehicleModelCreateDto itemDto)
        {
            var entity = _mapper.Map<VehicleModel>(itemDto);
            var createdItem = await _vehicleModelService.AddAsync(entity);
            var responseDto = _mapper.Map<VehicleModelResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<VehicleModelResponseDto>> Update(long id, [FromBody] VehicleModelUpdateDto itemDto)
        {
            var existingItem = await _vehicleModelService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _vehicleModelService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<VehicleModelResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _vehicleModelService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





