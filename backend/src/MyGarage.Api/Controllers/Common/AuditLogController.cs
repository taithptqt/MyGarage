using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Application.Audit;
using MyGarage.Application.Common.Pagination;

namespace MyGarage.Controllers
{
    [Route("api/v1/audit-logs")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;
        private readonly AdminAuditQuery _adminAuditQuery;

        public AuditLogController(IAuditLogService auditLogService, AdminAuditQuery adminAuditQuery)
        {
            _auditLogService = auditLogService;
            _adminAuditQuery = adminAuditQuery;
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
                paged => _auditLogService.MapToDtos(paged));
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AuditLogResponseDto>> GetById(long id)
        {
            var dto = await _auditLogService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public IActionResult Create()
        {
            Response.Headers.Allow = "GET";
            return this.ProblemMessage(405, "Nhật ký kiểm toán chỉ được hệ thống ghi.");
        }

        [HttpPut("{id}")]
        public IActionResult Update(long id)
        {
            Response.Headers.Allow = "GET";
            return this.ProblemMessage(405, "Nhật ký kiểm toán không được sửa trực tiếp.");
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(long id)
        {
            Response.Headers.Allow = "GET";
            return this.ProblemMessage(405, "Nhật ký kiểm toán không được xóa trực tiếp.");
        }
    }
}



