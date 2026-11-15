using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/inventory-audit-details")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class InventoryAuditDetailController : ControllerBase
    {
        private readonly IInventoryAuditDetailService _inventoryAuditDetailService;

        public InventoryAuditDetailController(IInventoryAuditDetailService inventoryAuditDetailService)
        {
            _inventoryAuditDetailService = inventoryAuditDetailService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryAuditDetailResponseDto>>> GetAll()
        {
            var dtos = await _inventoryAuditDetailService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryAuditDetailResponseDto>> GetById(long id)
        {
            var dto = await _inventoryAuditDetailService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InventoryAuditDetailResponseDto>> Create([FromBody] InventoryAuditDetailCreateDto itemDto)
        {
            var responseDto = await _inventoryAuditDetailService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InventoryAuditDetailResponseDto>> Update(long id, [FromBody] InventoryAuditDetailUpdateDto itemDto)
        {
            var responseDto = await _inventoryAuditDetailService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
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






