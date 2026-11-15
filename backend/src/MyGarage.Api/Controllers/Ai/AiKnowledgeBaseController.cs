using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/ai-knowledge-bases")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class AiKnowledgeBaseController : ControllerBase
    {
        private readonly IAiKnowledgeBaseService _aiKnowledgeBaseService;

        public AiKnowledgeBaseController(IAiKnowledgeBaseService aiKnowledgeBaseService)
        {
            _aiKnowledgeBaseService = aiKnowledgeBaseService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AiKnowledgeBaseResponseDto>>> GetAll()
        {
            var dtos = await _aiKnowledgeBaseService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiKnowledgeBaseResponseDto>> GetById(long id)
        {
            var dto = await _aiKnowledgeBaseService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiKnowledgeBaseResponseDto>> Create([FromBody] AiKnowledgeBaseCreateDto itemDto)
        {
            var responseDto = await _aiKnowledgeBaseService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiKnowledgeBaseResponseDto>> Update(long id, [FromBody] AiKnowledgeBaseUpdateDto itemDto)
        {
            var responseDto = await _aiKnowledgeBaseService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
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






