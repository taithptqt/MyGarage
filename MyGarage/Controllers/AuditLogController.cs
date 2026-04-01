using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/audit-logs")]
    [ApiController]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;
        private readonly IMapper _mapper;

        public AuditLogController(IAuditLogService auditLogService, IMapper mapper)
        {
            _auditLogService = auditLogService;
            _mapper = mapper;
        }

        [HttpGet("administrative-change-entries")]
        public async Task<IActionResult> GetAdminChanges(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? action = null,
            [FromQuery] string? tableName = null,
            [FromQuery] string? query = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var (items, total) = await _auditLogService.GetAdminChangesAsync(
                page,
                pageSize,
                action,
                tableName,
                query,
                from,
                to);

            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 1 : (pageSize > 200 ? 200 : pageSize);

            var payloadItems = items
                .Select(x => new
                {
                    id = x.Id,
                    userId = x.UserId,
                    userType = x.UserType,
                    branchId = x.BranchId,
                    action = x.Action,
                    tableName = x.TableName,
                    recordId = x.RecordId,
                    oldValue = x.OldValue,
                    newValue = x.NewValue,
                    ipAddress = x.IpAddress,
                    userAgent = x.UserAgent,
                    sessionId = x.SessionId,
                    timestamp = x.Timestamp
                })
                .ToList();

            return Ok(new { items = payloadItems, total, page, pageSize });
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AuditLogResponseDto>>> GetAll()
        {
            var items = await _auditLogService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AuditLogResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AuditLogResponseDto>> GetById(long id)
        {
            var item = await _auditLogService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AuditLogResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AuditLogResponseDto>> Create([FromBody] AuditLogCreateDto itemDto)
        {
            var entity = _mapper.Map<AuditLog>(itemDto);
            var createdItem = await _auditLogService.AddAsync(entity);
            var responseDto = _mapper.Map<AuditLogResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AuditLogResponseDto>> Update(long id, [FromBody] AuditLogUpdateDto itemDto)
        {
            var existingItem = await _auditLogService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _auditLogService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AuditLogResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _auditLogService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}




