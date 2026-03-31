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
    [Route("api/retail-orders")]
[Route("api/[controller]")]
    [ApiController]
    public class RetailOrderController : ControllerBase
    {
        private readonly IRetailOrderService _retailOrderService;
        private readonly IMapper _mapper;

        public RetailOrderController(IRetailOrderService retailOrderService, IMapper mapper)
        {
            _retailOrderService = retailOrderService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RetailOrderResponseDto>>> GetAll()
        {
            var items = await _retailOrderService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<RetailOrderResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailOrderResponseDto>> GetById(long id)
        {
            var item = await _retailOrderService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<RetailOrderResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailOrderResponseDto>> Create([FromBody] RetailOrderCreateDto itemDto)
        {
            var entity = _mapper.Map<RetailOrder>(itemDto);
            var createdItem = await _retailOrderService.AddAsync(entity);
            var responseDto = _mapper.Map<RetailOrderResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailOrderResponseDto>> Update(long id, [FromBody] RetailOrderUpdateDto itemDto)
        {
            var existingItem = await _retailOrderService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _retailOrderService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<RetailOrderResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _retailOrderService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






