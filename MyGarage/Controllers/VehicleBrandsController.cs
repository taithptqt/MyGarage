using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/vehicle-brands")]
    [ApiController]
    public class VehicleBrandsController : ControllerBase
    {
        private readonly IVehicleBrandService _vehicleBrandService;
        private readonly IMapper _mapper;

        public VehicleBrandsController(IVehicleBrandService vehicleBrandService, IMapper mapper)
        {
            _vehicleBrandService = vehicleBrandService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleBrandResponseDto>>> GetAll()
        {
            var items = await _vehicleBrandService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<VehicleBrandResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VehicleBrandResponseDto>> GetById(long id)
        {
            var item = await _vehicleBrandService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<VehicleBrandResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleBrandResponseDto>> Create([FromBody] VehicleBrandCreateDto itemDto)
        {
            var entity = _mapper.Map<VehicleBrand>(itemDto);
            var createdItem = await _vehicleBrandService.AddAsync(entity);
            var responseDto = _mapper.Map<VehicleBrandResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<VehicleBrandResponseDto>> Update(long id, [FromBody] VehicleBrandUpdateDto itemDto)
        {
            var existingItem = await _vehicleBrandService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _vehicleBrandService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<VehicleBrandResponseDto>(updatedItem);
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
