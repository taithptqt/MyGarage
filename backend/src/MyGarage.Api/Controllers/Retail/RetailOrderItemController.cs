using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/retail-order-items")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class RetailOrderItemController : ControllerBase
    {
        private readonly IRetailOrderItemService _retailOrderItemService;

        public RetailOrderItemController(IRetailOrderItemService retailOrderItemService)
        {
            _retailOrderItemService = retailOrderItemService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RetailOrderItemResponseDto>>> GetAll()
        {
            var dtos = await _retailOrderItemService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailOrderItemResponseDto>> GetById(long id)
        {
            var dto = await _retailOrderItemService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailOrderItemResponseDto>> Create([FromBody] RetailOrderItemCreateDto itemDto)
        {
            var responseDto = await _retailOrderItemService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailOrderItemResponseDto>> Update(long id, [FromBody] RetailOrderItemUpdateDto itemDto)
        {
            var responseDto = await _retailOrderItemService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _retailOrderItemService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






