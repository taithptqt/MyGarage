using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/warehouses")]
    [Route("api/v1/warehouses")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class WarehouseController : ControllerBase
    {
        private readonly IWarehouseService _warehouseService;
        private readonly IMapper _mapper;

        public WarehouseController(IWarehouseService warehouseService, IMapper mapper)
        {
            _warehouseService = warehouseService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WarehouseResponseDto>>> GetAll()
        {
            var items = await _warehouseService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<WarehouseResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WarehouseResponseDto>> GetById(long id)
        {
            var item = await _warehouseService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<WarehouseResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<WarehouseResponseDto>> Create([FromBody] WarehouseCreateDto itemDto)
        {
            var entity = _mapper.Map<Warehouse>(itemDto);
            var createdItem = await _warehouseService.AddAsync(entity);
            var responseDto = _mapper.Map<WarehouseResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<WarehouseResponseDto>> Update(long id, [FromBody] WarehouseUpdateDto itemDto)
        {
            var existingItem = await _warehouseService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _warehouseService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<WarehouseResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _warehouseService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






