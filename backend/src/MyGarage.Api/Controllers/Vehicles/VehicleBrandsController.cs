using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/vehicle-brands")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class VehicleBrandsController : ControllerBase
    {
        private readonly IVehicleBrandService _vehicleBrandService;

        public VehicleBrandsController(IVehicleBrandService vehicleBrandService)
        {
            _vehicleBrandService = vehicleBrandService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleBrandResponseDto>>> GetAll()
        {
            var dtos = await _vehicleBrandService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [AllowAnonymous]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<VehicleBrandResponseDto>> GetById(long id)
        {
            var dto = await _vehicleBrandService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleBrandResponseDto>> Create([FromBody] VehicleBrandCreateDto itemDto)
        {
            var responseDto = await _vehicleBrandService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<VehicleBrandResponseDto>> Update(long id, [FromBody] VehicleBrandUpdateDto itemDto)
        {
            var responseDto = await _vehicleBrandService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }

            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _vehicleBrandService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
