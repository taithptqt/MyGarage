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
    [Route("api/retail-return-items")]
[Route("api/[controller]")]
    [ApiController]
    public class RetailReturnItemController : ControllerBase
    {
        private readonly IRetailReturnItemService _retailReturnItemService;
        private readonly IMapper _mapper;

        public RetailReturnItemController(IRetailReturnItemService retailReturnItemService, IMapper mapper)
        {
            _retailReturnItemService = retailReturnItemService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RetailReturnItemResponseDto>>> GetAll()
        {
            var items = await _retailReturnItemService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<RetailReturnItemResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailReturnItemResponseDto>> GetById(long id)
        {
            var item = await _retailReturnItemService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<RetailReturnItemResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailReturnItemResponseDto>> Create([FromBody] RetailReturnItemCreateDto itemDto)
        {
            var entity = _mapper.Map<RetailReturnItem>(itemDto);
            var createdItem = await _retailReturnItemService.AddAsync(entity);
            var responseDto = _mapper.Map<RetailReturnItemResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailReturnItemResponseDto>> Update(long id, [FromBody] RetailReturnItemUpdateDto itemDto)
        {
            var existingItem = await _retailReturnItemService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _retailReturnItemService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<RetailReturnItemResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _retailReturnItemService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






