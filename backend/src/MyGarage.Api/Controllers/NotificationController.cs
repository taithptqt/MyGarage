using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/notifications")]
    [Route("api/v1/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public NotificationController(INotificationService notificationService, IMapper mapper, IGarageContext context)
        {
            _notificationService = notificationService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<NotificationResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var query = _context.Notifications.AsNoTracking().AsQueryable();
            if (!this.IsStaffUser())
            {
                var currentCustomerId = this.GetCurrentUserId();
                if (!currentCustomerId.HasValue)
                {
                    return Forbid();
                }

                query = query.Where(x => x.CustomerId == currentCustomerId.Value);
            }

            var envelope = await query
                .OrderByDescending(x => x.Id)
                .ToPagedEnvelopeAsync(page, pageSize, paged => _mapper.Map<IEnumerable<NotificationResponseDto>>(paged));
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

            var dto = _mapper.Map<NotificationResponseDto>(item);
            return Ok(dto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<NotificationResponseDto>> Create([FromBody] NotificationCreateDto itemDto)
        {
            var entity = _mapper.Map<Notification>(itemDto);
            var createdItem = await _notificationService.AddAsync(entity);
            var responseDto = _mapper.Map<NotificationResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
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
            _mapper.Map(itemDto, existingItem);
            if (!this.IsStaffUser())
            {
                existingItem.CustomerId = preservedCustomerId;
            }

            var updatedItem = await _notificationService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<NotificationResponseDto>(updatedItem);
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






