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
    [Route("api/retail-order-items")]
    [Route("api/v1/retail-order-items")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class RetailOrderItemController : ControllerBase
    {
        private readonly IRetailOrderItemService _retailOrderItemService;
        private readonly IMapper _mapper;

        public RetailOrderItemController(IRetailOrderItemService retailOrderItemService, IMapper mapper)
        {
            _retailOrderItemService = retailOrderItemService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RetailOrderItemResponseDto>>> GetAll()
        {
            var items = await _retailOrderItemService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<RetailOrderItemResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailOrderItemResponseDto>> GetById(long id)
        {
            var item = await _retailOrderItemService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<RetailOrderItemResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailOrderItemResponseDto>> Create([FromBody] RetailOrderItemCreateDto itemDto)
        {
            var entity = _mapper.Map<RetailOrderItem>(itemDto);
            var createdItem = await _retailOrderItemService.AddAsync(entity);
            var responseDto = _mapper.Map<RetailOrderItemResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailOrderItemResponseDto>> Update(long id, [FromBody] RetailOrderItemUpdateDto itemDto)
        {
            var existingItem = await _retailOrderItemService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _retailOrderItemService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<RetailOrderItemResponseDto>(updatedItem);
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






