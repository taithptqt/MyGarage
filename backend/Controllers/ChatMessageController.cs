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
    [Route("api/chat-messages")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class ChatMessageController : ControllerBase
    {
        private readonly IChatMessageService _chatMessageService;
        private readonly IMapper _mapper;

        public ChatMessageController(IChatMessageService chatMessageService, IMapper mapper)
        {
            _chatMessageService = chatMessageService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ChatMessageResponseDto>>> GetAll()
        {
            var items = await _chatMessageService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ChatMessageResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ChatMessageResponseDto>> GetById(long id)
        {
            var item = await _chatMessageService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ChatMessageResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ChatMessageResponseDto>> Create([FromBody] ChatMessageCreateDto itemDto)
        {
            var entity = _mapper.Map<ChatMessage>(itemDto);
            var createdItem = await _chatMessageService.AddAsync(entity);
            var responseDto = _mapper.Map<ChatMessageResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ChatMessageResponseDto>> Update(long id, [FromBody] ChatMessageUpdateDto itemDto)
        {
            var existingItem = await _chatMessageService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _chatMessageService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ChatMessageResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _chatMessageService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






