// Mục đích: Cung cấp API quản lý luồng phê duyệt ngoại lệ khi áp dụng voucher.
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/voucher-approval-requests")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class VoucherApprovalRequestController : ControllerBase
    {
        public sealed record VoucherApprovalDecisionPatchDto
        {
            public long ApproverId { get; init; }
            public string? Decision { get; init; }
            public string? Reason { get; init; }
            public string? Note { get; init; }
        }

        private readonly IVoucherApprovalRequestService _service;
        private readonly IVoucherApprovalRequestQueryService _queryService;

        public VoucherApprovalRequestController(
            IVoucherApprovalRequestService service,
            IVoucherApprovalRequestQueryService queryService)
        {
            _service = service;
            _queryService = queryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<VoucherApprovalRequestResponseDto>>> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _queryService.GetPagedAsync(status, normalizedPage, normalizedSize);
            var dtos = _service.MapToDtos(paged);
            return Ok(PagedEnvelope<VoucherApprovalRequestResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("admin-page")]
        public async Task<IActionResult> GetAdminPage([FromQuery] string? search, [FromQuery] int? page,
            [FromQuery] int? pageSize, System.Threading.CancellationToken cancellationToken)
        {
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
            var (rows, total) = await _queryService.GetAdminPageAsync(search, current, size, cancellationToken);
            return Ok(new { items = _service.MapToDtos(rows), total });
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VoucherApprovalRequestResponseDto>> GetById(long id)
        {
            var dto = await _service.GetDetailedDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<VoucherApprovalRequestResponseDto>> Create([FromBody] VoucherApprovalRequestCreateDto dto)
        {
            if (dto.RequestedBy <= 0 || string.IsNullOrWhiteSpace(dto.RequestType) || string.IsNullOrWhiteSpace(dto.Reason))
            {
                return this.BadRequestProblem("RequestedBy, RequestType, and Reason are required.");
            }

            var response = await _service.CreateDtoAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        [HttpPatch("{id:long}")]
        public async Task<ActionResult<VoucherApprovalRequestResponseDto>> PatchDecision(long id, [FromBody] VoucherApprovalDecisionPatchDto decision)
        {
            if (decision.ApproverId <= 0)
            {
                return this.BadRequestProblem("ApproverId is required.");
            }

            var normalizedDecision = (decision.Decision ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedDecision is not ("APPROVE" or "REJECT"))
            {
                return this.BadRequestProblem("Decision must be APPROVE or REJECT.");
            }

            var mapped = new VoucherApprovalDecisionDto
            {
                ApproverId = decision.ApproverId,
                Reason = decision.Reason,
                Note = decision.Note,
            };

            try
            {
                var updated = normalizedDecision == "APPROVE"
                    ? await _service.ApproveDtoAsync(id, mapped)
                    : await _service.RejectDtoAsync(id, mapped);

                if (updated == null)
                {
                    return NotFound();
                }

                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

    }
}

