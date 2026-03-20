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
    public class VehicleConditionLogController : ControllerBase
    {
        private readonly IVehicleConditionLogService _vehicleConditionLogService;
        private readonly IMapper _mapper;

        public VehicleConditionLogController(IVehicleConditionLogService vehicleConditionLogService, IMapper mapper)
        {
            _vehicleConditionLogService = vehicleConditionLogService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleConditionLogResponseDto>>> GetAll()
        {
            var items = await _vehicleConditionLogService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<VehicleConditionLogResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> GetById(long id)
        {
            var item = await _vehicleConditionLogService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<VehicleConditionLogResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> Create([FromBody] VehicleConditionLogCreateDto itemDto)
        {
            var entity = _mapper.Map<VehicleConditionLog>(itemDto);
            var createdItem = await _vehicleConditionLogService.AddAsync(entity);
            var responseDto = _mapper.Map<VehicleConditionLogResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> Update(long id, [FromBody] VehicleConditionLogUpdateDto itemDto)
        {
            var existingItem = await _vehicleConditionLogService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _vehicleConditionLogService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<VehicleConditionLogResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _vehicleConditionLogService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





