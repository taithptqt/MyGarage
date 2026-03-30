// Mục đích: Cung cấp API quản lý luồng phê duyệt ngoại lệ khi áp dụng voucher.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/voucher-approval-requests")]
    [ApiController]
    public class VoucherApprovalRequestController : ControllerBase
    {
        private readonly IVoucherApprovalRequestService _service;
        private readonly IMapper _mapper;

        public VoucherApprovalRequestController(IVoucherApprovalRequestService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VoucherApprovalRequestResponseDto>>> GetAll([FromQuery] string? status = null)
        {
            var items = await _service.GetAllAsync(status);
            var dtos = _mapper.Map<IEnumerable<VoucherApprovalRequestResponseDto>>(items);
            return Ok(dtos);
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

        [HttpPost("{id:long}/approve")]
        public async Task<ActionResult<VoucherApprovalRequestResponseDto>> Approve(long id, [FromBody] VoucherApprovalDecisionDto decision)
        {
            if (decision.ApproverId <= 0)
            {
                return BadRequest(new { message = "ApproverId is required." });
            }

            try
            {
                var updated = await _service.ApproveAsync(id, decision);
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

        [HttpPost("{id:long}/reject")]
        public async Task<ActionResult<VoucherApprovalRequestResponseDto>> Reject(long id, [FromBody] VoucherApprovalDecisionDto decision)
        {
            if (decision.ApproverId <= 0)
            {
                return BadRequest(new { message = "ApproverId is required." });
            }

            if (string.IsNullOrWhiteSpace(decision.Reason) && string.IsNullOrWhiteSpace(decision.Note))
            {
                return BadRequest(new { message = "Reason or note is required for rejection." });
            }

            try
            {
                var updated = await _service.RejectAsync(id, decision);
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

