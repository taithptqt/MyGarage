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
    public class InventoryAuditController : ControllerBase
    {
        private readonly IInventoryAuditService _inventoryAuditService;
        private readonly IMapper _mapper;

        public InventoryAuditController(IInventoryAuditService inventoryAuditService, IMapper mapper)
        {
            _inventoryAuditService = inventoryAuditService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryAuditResponseDto>>> GetAll()
        {
            var items = await _inventoryAuditService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<InventoryAuditResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryAuditResponseDto>> GetById(long id)
        {
            var item = await _inventoryAuditService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<InventoryAuditResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InventoryAuditResponseDto>> Create([FromBody] InventoryAuditCreateDto itemDto)
        {
            var entity = _mapper.Map<InventoryAudit>(itemDto);
            var createdItem = await _inventoryAuditService.AddAsync(entity);
            var responseDto = _mapper.Map<InventoryAuditResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InventoryAuditResponseDto>> Update(long id, [FromBody] InventoryAuditUpdateDto itemDto)
        {
            var existingItem = await _inventoryAuditService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _inventoryAuditService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<InventoryAuditResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _inventoryAuditService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





