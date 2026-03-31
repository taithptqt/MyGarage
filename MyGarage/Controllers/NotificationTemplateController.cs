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
    [Route("api/notification-templates")]
[Route("api/[controller]")]
    [ApiController]
    public class NotificationTemplateController : ControllerBase
    {
        private readonly INotificationTemplateService _notificationTemplateService;
        private readonly IMapper _mapper;

        public NotificationTemplateController(INotificationTemplateService notificationTemplateService, IMapper mapper)
        {
            _notificationTemplateService = notificationTemplateService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationTemplateResponseDto>>> GetAll()
        {
            var items = await _notificationTemplateService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<NotificationTemplateResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NotificationTemplateResponseDto>> GetById(long id)
        {
            var item = await _notificationTemplateService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<NotificationTemplateResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<NotificationTemplateResponseDto>> Create([FromBody] NotificationTemplateCreateDto itemDto)
        {
            var entity = _mapper.Map<NotificationTemplate>(itemDto);
            var createdItem = await _notificationTemplateService.AddAsync(entity);
            var responseDto = _mapper.Map<NotificationTemplateResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<NotificationTemplateResponseDto>> Update(long id, [FromBody] NotificationTemplateUpdateDto itemDto)
        {
            var existingItem = await _notificationTemplateService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _notificationTemplateService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<NotificationTemplateResponseDto>(updatedItem);
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






