using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/notification-templates")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class NotificationTemplateController : ControllerBase
    {
        private readonly INotificationTemplateService _notificationTemplateService;

        public NotificationTemplateController(INotificationTemplateService notificationTemplateService)
        {
            _notificationTemplateService = notificationTemplateService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationTemplateResponseDto>>> GetAll()
        {
            var dtos = await _notificationTemplateService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NotificationTemplateResponseDto>> GetById(long id)
        {
            var dto = await _notificationTemplateService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<NotificationTemplateResponseDto>> Create([FromBody] NotificationTemplateCreateDto itemDto)
        {
            var responseDto = await _notificationTemplateService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<NotificationTemplateResponseDto>> Update(long id, [FromBody] NotificationTemplateUpdateDto itemDto)
        {
            var responseDto = await _notificationTemplateService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _notificationTemplateService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






