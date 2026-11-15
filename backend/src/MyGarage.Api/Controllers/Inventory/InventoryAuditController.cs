using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/v1/inventory-audits")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class InventoryAuditController : ControllerBase
    {
        private readonly IInventoryAuditService _inventoryAuditService;

        public InventoryAuditController(IInventoryAuditService inventoryAuditService)
        {
            _inventoryAuditService = inventoryAuditService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<InventoryAuditResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _inventoryAuditService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                _inventoryAuditService.MapToDtos);
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryAuditResponseDto>> GetById(long id)
        {
            var dto = await _inventoryAuditService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InventoryAuditResponseDto>> Create([FromBody] InventoryAuditCreateDto itemDto)
        {
            var responseDto = await _inventoryAuditService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InventoryAuditResponseDto>> Update(long id, [FromBody] InventoryAuditUpdateDto itemDto)
        {
            var responseDto = await _inventoryAuditService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
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






