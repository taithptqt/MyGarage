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
    [Route("api/ai-knowledge-bases")]
    [Route("api/v1/ai-knowledge-bases")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class AiKnowledgeBaseController : ControllerBase
    {
        private readonly IAiKnowledgeBaseService _aiKnowledgeBaseService;
        private readonly IMapper _mapper;

        public AiKnowledgeBaseController(IAiKnowledgeBaseService aiKnowledgeBaseService, IMapper mapper)
        {
            _aiKnowledgeBaseService = aiKnowledgeBaseService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AiKnowledgeBaseResponseDto>>> GetAll()
        {
            var items = await _aiKnowledgeBaseService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AiKnowledgeBaseResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiKnowledgeBaseResponseDto>> GetById(long id)
        {
            var item = await _aiKnowledgeBaseService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiKnowledgeBaseResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiKnowledgeBaseResponseDto>> Create([FromBody] AiKnowledgeBaseCreateDto itemDto)
        {
            var entity = _mapper.Map<AiKnowledgeBase>(itemDto);
            var createdItem = await _aiKnowledgeBaseService.AddAsync(entity);
            var responseDto = _mapper.Map<AiKnowledgeBaseResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiKnowledgeBaseResponseDto>> Update(long id, [FromBody] AiKnowledgeBaseUpdateDto itemDto)
        {
            var existingItem = await _aiKnowledgeBaseService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiKnowledgeBaseService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiKnowledgeBaseResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiKnowledgeBaseService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






