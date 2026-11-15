// Mục đích: Cung cấp API quản lý luồng phê duyệt ngoại lệ khi áp dụng voucher.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/voucher-approval-requests")]
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
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public VoucherApprovalRequestController(IVoucherApprovalRequestService service, IMapper mapper, IGarageContext context)
        {
            _service = service;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<VoucherApprovalRequestResponseDto>>> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var query = _context.VoucherApprovalRequests.AsNoTracking().Include(x => x.Voucher).Include(x => x.Redemption).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                query = query.Where(x => x.Status != null && x.Status.ToUpper() == normalized);
            }
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var paged = offset >= total
                ? new List<VoucherApprovalRequest>()
                : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                    .Skip((int)offset).Take(normalizedSize).ToListAsync();
            var dtos = _mapper.Map<List<VoucherApprovalRequestResponseDto>>(paged);
            return Ok(PagedEnvelope<VoucherApprovalRequestResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("admin-page")]
        public async Task<IActionResult> GetAdminPage([FromQuery] string? search, [FromQuery] int? page,
            [FromQuery] int? pageSize, System.Threading.CancellationToken cancellationToken)
        {
            var query = _context.VoucherApprovalRequests.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToUpperInvariant();
                query = query.Where(x => ((x.RequestType ?? "null") + " " + (x.Reason ?? "null") + " " + x.Id.ToString() + " " +
                    (x.VoucherId.HasValue ? x.VoucherId.Value.ToString() : "null")).ToUpper().Contains(term));
            }
            var total = await query.CountAsync(cancellationToken);
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
            var offset = ((long)current - 1) * size;
            var rows = offset >= total ? new List<VoucherApprovalRequest>() :
                await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                    .Skip((int)offset).Take(size).ToListAsync(cancellationToken);
            return Ok(new { items = _mapper.Map<List<VoucherApprovalRequestResponseDto>>(rows), total });
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VoucherApprovalRequestResponseDto>> GetById(long id)
        {
            var item = await _service.GetDetailedByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<VoucherApprovalRequestResponseDto>(item));
        }

        [HttpPost]
        public async Task<ActionResult<VoucherApprovalRequestResponseDto>> Create([FromBody] VoucherApprovalRequestCreateDto dto)
        {
            if (dto.RequestedBy <= 0 || string.IsNullOrWhiteSpace(dto.RequestType) || string.IsNullOrWhiteSpace(dto.Reason))
            {
                return BadRequest(new { message = "RequestedBy, RequestType, and Reason are required." });
            }

            var entity = _mapper.Map<VoucherApprovalRequest>(dto);
            var created = await _service.AddAsync(entity);
            var response = _mapper.Map<VoucherApprovalRequestResponseDto>(created);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, response);
        }

        [HttpPatch("{id:long}")]
        public async Task<ActionResult<VoucherApprovalRequestResponseDto>> PatchDecision(long id, [FromBody] VoucherApprovalDecisionPatchDto decision)
        {
            if (decision.ApproverId <= 0)
            {
                return BadRequest(new { message = "ApproverId is required." });
            }

            var normalizedDecision = (decision.Decision ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedDecision is not ("APPROVE" or "REJECT"))
            {
                return BadRequest(new { message = "Decision must be APPROVE or REJECT." });
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
                    ? await _service.ApproveAsync(id, mapped)
                    : await _service.RejectAsync(id, mapped);

                if (updated == null)
                {
                    return NotFound();
                }

                return Ok(_mapper.Map<VoucherApprovalRequestResponseDto>(updated));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

    }
}

