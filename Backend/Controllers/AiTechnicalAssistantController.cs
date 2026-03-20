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
    public class AiTechnicalAssistantController : ControllerBase
    {
        private readonly IAiTechnicalAssistantService _aiTechnicalAssistantService;
        private readonly IMapper _mapper;

        public AiTechnicalAssistantController(IAiTechnicalAssistantService aiTechnicalAssistantService, IMapper mapper)
        {
            _aiTechnicalAssistantService = aiTechnicalAssistantService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AiTechnicalAssistantResponseDto>>> GetAll()
        {
            var items = await _aiTechnicalAssistantService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AiTechnicalAssistantResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiTechnicalAssistantResponseDto>> GetById(long id)
        {
            var item = await _aiTechnicalAssistantService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiTechnicalAssistantResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiTechnicalAssistantResponseDto>> Create([FromBody] AiTechnicalAssistantCreateDto itemDto)
        {
            var entity = _mapper.Map<AiTechnicalAssistant>(itemDto);
            var createdItem = await _aiTechnicalAssistantService.AddAsync(entity);
            var responseDto = _mapper.Map<AiTechnicalAssistantResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiTechnicalAssistantResponseDto>> Update(long id, [FromBody] AiTechnicalAssistantUpdateDto itemDto)
        {
            var existingItem = await _aiTechnicalAssistantService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiTechnicalAssistantService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiTechnicalAssistantResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiTechnicalAssistantService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





