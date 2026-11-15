using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/inventories")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<InventoryResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _inventoryService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                _inventoryService.MapToDtos);
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryResponseDto>> GetById(long id)
        {
            var dto = await _inventoryService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InventoryResponseDto>> Create([FromBody] InventoryCreateDto itemDto)
        {
            var responseDto = await _inventoryService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InventoryResponseDto>> Update(long id, [FromBody] InventoryUpdateDto itemDto)
        {
            var responseDto = await _inventoryService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _inventoryService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






