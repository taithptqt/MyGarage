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
    public class InventoryAuditDetailController : ControllerBase
    {
        private readonly IInventoryAuditDetailService _inventoryAuditDetailService;
        private readonly IMapper _mapper;

        public InventoryAuditDetailController(IInventoryAuditDetailService inventoryAuditDetailService, IMapper mapper)
        {
            _inventoryAuditDetailService = inventoryAuditDetailService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryAuditDetailResponseDto>>> GetAll()
        {
            var items = await _inventoryAuditDetailService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<InventoryAuditDetailResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryAuditDetailResponseDto>> GetById(long id)
        {
            var item = await _inventoryAuditDetailService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<InventoryAuditDetailResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InventoryAuditDetailResponseDto>> Create([FromBody] InventoryAuditDetailCreateDto itemDto)
        {
            var entity = _mapper.Map<InventoryAuditDetail>(itemDto);
            var createdItem = await _inventoryAuditDetailService.AddAsync(entity);
            var responseDto = _mapper.Map<InventoryAuditDetailResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InventoryAuditDetailResponseDto>> Update(long id, [FromBody] InventoryAuditDetailUpdateDto itemDto)
        {
            var existingItem = await _inventoryAuditDetailService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _inventoryAuditDetailService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<InventoryAuditDetailResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _inventoryAuditDetailService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





