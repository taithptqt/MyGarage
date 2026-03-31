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
    [Route("api/vehicle-insurances")]
[Route("api/[controller]")]
    [ApiController]
    public class VehicleInsuranceController : ControllerBase
    {
        private readonly IVehicleInsuranceService _vehicleInsuranceService;
        private readonly IMapper _mapper;

        public VehicleInsuranceController(IVehicleInsuranceService vehicleInsuranceService, IMapper mapper)
        {
            _vehicleInsuranceService = vehicleInsuranceService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleInsuranceResponseDto>>> GetAll()
        {
            var items = await _vehicleInsuranceService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<VehicleInsuranceResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VehicleInsuranceResponseDto>> GetById(long id)
        {
            var item = await _vehicleInsuranceService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<VehicleInsuranceResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleInsuranceResponseDto>> Create([FromBody] VehicleInsuranceCreateDto itemDto)
        {
            var entity = _mapper.Map<VehicleInsurance>(itemDto);
            var createdItem = await _vehicleInsuranceService.AddAsync(entity);
            var responseDto = _mapper.Map<VehicleInsuranceResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<VehicleInsuranceResponseDto>> Update(long id, [FromBody] VehicleInsuranceUpdateDto itemDto)
        {
            var existingItem = await _vehicleInsuranceService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _vehicleInsuranceService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<VehicleInsuranceResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _vehicleInsuranceService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






