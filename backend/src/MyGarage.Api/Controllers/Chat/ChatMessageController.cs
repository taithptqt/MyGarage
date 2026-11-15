using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/chat-messages")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class ChatMessageController : ControllerBase
    {
        private readonly IChatMessageService _chatMessageService;

        public ChatMessageController(IChatMessageService chatMessageService)
        {
            _chatMessageService = chatMessageService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ChatMessageResponseDto>>> GetAll()
        {
            var dtos = await _chatMessageService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ChatMessageResponseDto>> GetById(long id)
        {
            var dto = await _chatMessageService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ChatMessageResponseDto>> Create([FromBody] ChatMessageCreateDto itemDto)
        {
            var responseDto = await _chatMessageService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ChatMessageResponseDto>> Update(long id, [FromBody] ChatMessageUpdateDto itemDto)
        {
            var responseDto = await _chatMessageService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
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






