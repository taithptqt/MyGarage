using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;
        private readonly GarageContext _context;
        private readonly IMapper _mapper;

        public AuditLogController(IAuditLogService auditLogService, GarageContext context, IMapper mapper)
        {
            _auditLogService = auditLogService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet("admin-changes")]
        public async Task<IActionResult> GetAdminChanges(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? action = null,
            [FromQuery] string? tableName = null,
            [FromQuery] string? query = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 1 : (pageSize > 200 ? 200 : pageSize);

            var logsQuery = _context.AuditLogs
                .AsNoTracking()
                .Where(x => x.TableName != null && x.TableName.ToUpper() != "AUDITLOGS");

            if (!string.IsNullOrWhiteSpace(action))
            {
                var normalized = action.Trim().ToUpperInvariant();
                logsQuery = logsQuery.Where(x => x.Action != null && x.Action.ToUpper() == normalized);
            }

            if (!string.IsNullOrWhiteSpace(tableName))
            {
                var normalized = tableName.Trim().ToUpperInvariant();
                logsQuery = logsQuery.Where(x => x.TableName != null && x.TableName.ToUpper().Contains(normalized));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalized = query.Trim().ToUpperInvariant();
                logsQuery = logsQuery.Where(x =>
                    (x.TableName != null && x.TableName.ToUpper().Contains(normalized)) ||
                    (x.Action != null && x.Action.ToUpper().Contains(normalized)) ||
                    (x.OldValue != null && x.OldValue.ToUpper().Contains(normalized)) ||
                    (x.NewValue != null && x.NewValue.ToUpper().Contains(normalized)));
            }

            if (from.HasValue)
            {
                logsQuery = logsQuery.Where(x => x.Timestamp.HasValue && x.Timestamp.Value >= from.Value);
            }

            if (to.HasValue)
            {
                logsQuery = logsQuery.Where(x => x.Timestamp.HasValue && x.Timestamp.Value <= to.Value);
            }

            var total = await logsQuery.CountAsync();
            var items = await logsQuery
                .OrderByDescending(x => x.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                .ToListAsync();

            return Ok(new { items, total, page, pageSize });
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



