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
    public class AiLearningFeedbackController : ControllerBase
    {
        private readonly IAiLearningFeedbackService _aiLearningFeedbackService;
        private readonly IMapper _mapper;

        public AiLearningFeedbackController(IAiLearningFeedbackService aiLearningFeedbackService, IMapper mapper)
        {
            _aiLearningFeedbackService = aiLearningFeedbackService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AiLearningFeedbackResponseDto>>> GetAll()
        {
            var items = await _aiLearningFeedbackService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AiLearningFeedbackResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiLearningFeedbackResponseDto>> GetById(long id)
        {
            var item = await _aiLearningFeedbackService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiLearningFeedbackResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiLearningFeedbackResponseDto>> Create([FromBody] AiLearningFeedbackCreateDto itemDto)
        {
            var entity = _mapper.Map<AiLearningFeedback>(itemDto);
            var createdItem = await _aiLearningFeedbackService.AddAsync(entity);
            var responseDto = _mapper.Map<AiLearningFeedbackResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiLearningFeedbackResponseDto>> Update(long id, [FromBody] AiLearningFeedbackUpdateDto itemDto)
        {
            var existingItem = await _aiLearningFeedbackService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiLearningFeedbackService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiLearningFeedbackResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiLearningFeedbackService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





