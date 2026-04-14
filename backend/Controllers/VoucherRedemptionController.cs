using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/voucher-redemptions")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class VoucherRedemptionController : ControllerBase
    {
        private readonly IVoucherRedemptionService _service;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public VoucherRedemptionController(IVoucherRedemptionService service, IGarageContext context, IMapper mapper)
        {
            _service = service;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VoucherRedemptionResponseDto>>> GetAll([FromQuery] string? status = null)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var items = await _service.GetAllAsync(status);
            if (scopedBranchId.HasValue)
            {
                items = items.Where(item => item.BranchId == scopedBranchId.Value).ToList();
            }

            var dtos = _mapper.Map<IEnumerable<VoucherRedemptionResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VoucherRedemptionResponseDto>> GetById(long id)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var item = await _service.GetDetailedByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && item.BranchId != scopedBranchId.Value)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<VoucherRedemptionResponseDto>(item));
        }

        [HttpPost("validations")]
        public async Task<ActionResult<VoucherValidationResultResponseDto>> Validate([FromBody] VoucherRedemptionRequestDto request)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            request ??= new VoucherRedemptionRequestDto();
            request.AppliedBy = actorUserId.Value;
            if (scopedBranchId.HasValue)
            {
                request.BranchId = scopedBranchId.Value;
            }

            var result = await _service.CheckEligibilityAsync(request);
            return result.IsEligible ? Ok(result) : BadRequest(result);
        }

        [HttpPost("transactions")]
        public async Task<ActionResult<VoucherRedeemResultResponseDto>> CreateTransaction([FromBody] VoucherRedemptionRequestDto request)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            request ??= new VoucherRedemptionRequestDto();
            request.AppliedBy = actorUserId.Value;
            if (scopedBranchId.HasValue)
            {
                request.BranchId = scopedBranchId.Value;
            }

            var result = await _service.RedeemAsync(request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            if (result.Redemption == null)
            {
                return Ok(result);
            }

            await WriteAuditAsync(
                actorUserId.Value,
                request.BranchId,
                "VOUCHER_REDEEMED",
                result.Redemption.Id,
                null,
                JsonSerializer.Serialize(new
                {
                    result.Redemption.VoucherId,
                    result.Redemption.BranchId,
                    result.Redemption.DiscountAmount,
                    result.Redemption.AppliedBy
                }));

            return CreatedAtAction(nameof(GetById), new { id = result.Redemption.Id }, result);
        }

        [HttpPost("transactions/{id:long}/reversals")]
        public async Task<ActionResult<VoucherRedeemResultResponseDto>> CreateReversal(long id, [FromBody] VoucherRedemptionReverseDto request)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var existing = await _service.GetDetailedByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && existing.BranchId != scopedBranchId.Value)
            {
                return NotFound();
            }

            request ??= new VoucherRedemptionReverseDto();
            request.ApprovedBy = actorUserId.Value;

            var result = await _service.ReverseAsync(id, request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            await WriteAuditAsync(
                actorUserId.Value,
                existing.BranchId,
                "VOUCHER_REDEMPTION_REVERSED",
                id,
                JsonSerializer.Serialize(new
                {
                    existing.RedemptionStatus,
                    existing.ApprovedBy
                }),
                JsonSerializer.Serialize(new
                {
                    result.Redemption?.RedemptionStatus,
                    request.ApprovedBy,
                    request.ApprovalRequestId,
                    request.Reason
                }));

            return Ok(result);
        }

        private async Task WriteAuditAsync(
            long actorUserId,
            long? branchId,
            string action,
            long recordId,
            string? oldValue,
            string? newValue)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                UserType = "STAFF",
                BranchId = branchId,
                Action = action,
                TableName = "VOUCHER_REDEMPTIONS",
                RecordId = recordId,
                OldValue = oldValue,
                NewValue = newValue,
                Timestamp = VietnamTime.Now
            });
            await _context.SaveChangesAsync();
        }

        private bool TryResolveFinanceBranchScope(out long? branchId, out ActionResult deniedResult)
        {
            branchId = null;
            deniedResult = null!;

            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                deniedResult = Unauthorized();
                return false;
            }

            if (this.RequiresFinanceBranchScope())
            {
                branchId = this.GetCurrentBranchId();
                if (!branchId.HasValue)
                {
                    deniedResult = Forbid();
                    return false;
                }
            }

            return true;
        }
    }
}
