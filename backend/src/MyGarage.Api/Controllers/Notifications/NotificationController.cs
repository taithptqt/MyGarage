using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Notifications;

namespace MyGarage.Controllers
{
    [Route("api/v1/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly INotificationApplicationService _notificationApplicationService;

        public NotificationController(
            INotificationService notificationService,
            INotificationApplicationService notificationApplicationService)
        {
            _notificationService = notificationService;
            _notificationApplicationService = notificationApplicationService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<NotificationResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            CancellationToken ct = default)
        {
            long? scopedCustomerId = null;
            if (!this.IsStaffUser())
            {
                var currentCustomerId = this.GetCurrentUserId();
                if (!currentCustomerId.HasValue)
                {
                    return Forbid();
                }

                scopedCustomerId = currentCustomerId.Value;
            }

            var envelope = await _notificationApplicationService.GetPagedNotificationsAsync(
                scopedCustomerId,
                page,
                pageSize,
                ct);
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NotificationResponseDto>> GetById(long id)
        {
            var item = await _notificationService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!this.IsStaffUser() && item.CustomerId != this.GetCurrentUserId())
            {
                return Forbid();
            }

            var dto = _notificationService.MapToDto(item);
            return Ok(dto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<NotificationResponseDto>> Create([FromBody] NotificationCreateDto itemDto)
        {
            var responseDto = await _notificationService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<NotificationResponseDto>> Update(long id, [FromBody] NotificationUpdateDto itemDto)
        {
            var existingItem = await _notificationService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (!this.IsStaffUser() && existingItem.CustomerId != this.GetCurrentUserId())
            {
                return Forbid();
            }

            var preservedCustomerId = existingItem.CustomerId;
            _notificationService.MapUpdate(itemDto, existingItem);
            if (!this.IsStaffUser())
            {
                existingItem.CustomerId = preservedCustomerId;
            }

            var updatedItem = await _notificationService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _notificationService.MapToDto(updatedItem);
            return Ok(responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _notificationService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






