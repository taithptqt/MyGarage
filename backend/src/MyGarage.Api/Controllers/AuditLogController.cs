using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Common;
using MyGarage.Extensions;
using MyGarage.Application.Audit;
using MyGarage.Application.Common.Pagination;

namespace MyGarage.Controllers
{
    [Route("api/audit-logs")]
    [Route("api/v1/audit-logs")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;
        private readonly AdminAuditQuery _adminAuditQuery;
        private readonly IMapper _mapper;

        public AuditLogController(IAuditLogService auditLogService, AdminAuditQuery adminAuditQuery, IMapper mapper)
        {
            _auditLogService = auditLogService;
            _adminAuditQuery = adminAuditQuery;
            _mapper = mapper;
        }

        [HttpGet("admin-changes")]
        [HttpGet("entries")]
        public async Task<IActionResult> GetAdminChanges(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? action = null,
            [FromQuery] string? tableName = null,
            [FromQuery] string? query = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var result = await _adminAuditQuery.ExecuteAsync(page, pageSize, action, tableName, query, from, to, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<AuditLogResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _auditLogService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                paged => _mapper.Map<IEnumerable<AuditLogResponseDto>>(paged));
            return Ok(envelope);
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



