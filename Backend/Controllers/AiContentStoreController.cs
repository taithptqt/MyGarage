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
    public class AiContentStoreController : ControllerBase
    {
        private readonly IAiContentStoreService _aiContentStoreService;
        private readonly IMapper _mapper;

        public AiContentStoreController(IAiContentStoreService aiContentStoreService, IMapper mapper)
        {
            _aiContentStoreService = aiContentStoreService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AiContentStoreResponseDto>>> GetAll()
        {
            var items = await _aiContentStoreService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AiContentStoreResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiContentStoreResponseDto>> GetById(long id)
        {
            var item = await _aiContentStoreService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiContentStoreResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiContentStoreResponseDto>> Create([FromBody] AiContentStoreCreateDto itemDto)
        {
            var entity = _mapper.Map<AiContentStore>(itemDto);
            var createdItem = await _aiContentStoreService.AddAsync(entity);
            var responseDto = _mapper.Map<AiContentStoreResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiContentStoreResponseDto>> Update(long id, [FromBody] AiContentStoreUpdateDto itemDto)
        {
            var existingItem = await _aiContentStoreService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiContentStoreService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiContentStoreResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiContentStoreService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





