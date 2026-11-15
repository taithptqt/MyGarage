using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/vehicle-insurances")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class VehicleInsuranceController : ControllerBase
    {
        private readonly IVehicleInsuranceService _vehicleInsuranceService;

        public VehicleInsuranceController(IVehicleInsuranceService vehicleInsuranceService)
        {
            _vehicleInsuranceService = vehicleInsuranceService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleInsuranceResponseDto>>> GetAll()
        {
            var dtos = await _vehicleInsuranceService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VehicleInsuranceResponseDto>> GetById(long id)
        {
            var dto = await _vehicleInsuranceService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleInsuranceResponseDto>> Create([FromBody] VehicleInsuranceCreateDto itemDto)
        {
            var responseDto = await _vehicleInsuranceService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<VehicleInsuranceResponseDto>> Update(long id, [FromBody] VehicleInsuranceUpdateDto itemDto)
        {
            var responseDto = await _vehicleInsuranceService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
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






