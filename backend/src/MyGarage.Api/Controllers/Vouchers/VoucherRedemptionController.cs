// Mục đích: Cung cấp API kiểm tra điều kiện, redeem, reverse và tra cứu lịch sử sử dụng voucher.
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

namespace MyGarage.Controllers
{
    [Route("api/voucher-redemptions")]
    [Route("api/v1/voucher-redemptions")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class VoucherRedemptionController : ControllerBase
    {
        private readonly IVoucherRedemptionService _service;
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public VoucherRedemptionController(IVoucherRedemptionService service, IMapper mapper, IGarageContext context)
        {
            _service = service;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<VoucherRedemptionResponseDto>>> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var query = _context.VoucherRedemptions.AsNoTracking().Include(x => x.Voucher).AsQueryable();
            if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                query = query.Where(x => x.RedemptionStatus != null && x.RedemptionStatus.ToUpper() == normalized);
            }
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var paged = offset >= total
                ? new List<MyGarage.Models.VoucherRedemption>()
                : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                    .Skip((int)offset).Take(normalizedSize).ToListAsync();
            var dtos = _mapper.Map<List<VoucherRedemptionResponseDto>>(paged);
            return Ok(PagedEnvelope<VoucherRedemptionResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        // Separate opt-in page preserves the legacy array/header contract.
        [HttpGet("admin-page")]
        public async Task<IActionResult> GetAdminPage([FromQuery] string? search, [FromQuery] string? sort,
            [FromQuery] int? page, [FromQuery] int? pageSize, System.Threading.CancellationToken cancellationToken)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return denied!;
            var query = _context.VoucherRedemptions.AsNoTracking().AsQueryable();
            if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToUpperInvariant();
                query = query.Where(x => ((x.VoucherCode ?? "null") + " " + x.Id.ToString() + " " +
                    (x.CustomerId.HasValue ? x.CustomerId.Value.ToString() : "null")).ToUpper().Contains(term));
            }
            var total = await query.CountAsync(cancellationToken);
            var totalDiscount = await query.SumAsync(x => (decimal?)x.DiscountAmount, cancellationToken) ?? 0m;
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
            var offset = ((long)current - 1) * size;
            var ordered = string.Equals(sort, "discountDesc", System.StringComparison.OrdinalIgnoreCase)
                ? query.OrderByDescending(x => x.DiscountAmount).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                : query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
            var rows = offset >= total ? new List<MyGarage.Models.VoucherRedemption>() :
                await ordered.Skip((int)offset).Take(size).ToListAsync(cancellationToken);
            return Ok(new { items = _mapper.Map<List<VoucherRedemptionResponseDto>>(rows), total, totalDiscount });
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VoucherRedemptionResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var item = await _service.GetDetailedByIdAsync(id);
            if (item == null || (branchId.HasValue && item.BranchId != branchId.Value))
            {
                return NotFound();
            }

            return Ok(_mapper.Map<VoucherRedemptionResponseDto>(item));
        }

        [HttpPost("validations")]
        public async Task<ActionResult<VoucherValidationResultResponseDto>> Validate([FromBody] VoucherRedemptionRequestDto request)
        {
            var scopeError = await ValidateRequestScopeAsync(request);
            if (scopeError != null) return (ActionResult)scopeError;
            var result = await _service.CheckEligibilityAsync(request);
            return result.IsEligible ? Ok(result) : BadRequest(result);
        }

        [HttpPost("transactions")]
        public async Task<ActionResult<VoucherRedeemResultResponseDto>> CreateTransaction([FromBody] VoucherRedemptionRequestDto request)
        {
            var scopeError = await ValidateRequestScopeAsync(request);
            if (scopeError != null) return (ActionResult)scopeError;
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var key = request.IdempotencyKey.Trim();
                var existing = await _context.VoucherRedemptions.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == key);
                if (existing != null &&
                    (existing.BranchId != request.BranchId || existing.AppliedBy != request.AppliedBy ||
                     existing.CustomerId != request.CustomerId || existing.VehicleId != request.VehicleId ||
                     existing.ServiceOrderId != request.ServiceOrderId || existing.RetailOrderId != request.RetailOrderId ||
                     existing.InvoiceId != request.InvoiceId || existing.PaymentMethodId != request.PaymentMethodId ||
                     (request.VoucherId.HasValue && existing.VoucherId != request.VoucherId.Value) ||
                     (!string.IsNullOrWhiteSpace(request.VoucherCode) && !string.Equals(existing.VoucherCode, request.VoucherCode.Trim(), System.StringComparison.OrdinalIgnoreCase)) ||
                     (!request.InvoiceId.HasValue && request.EligibleAmount.HasValue && existing.EligibleAmount != request.EligibleAmount.Value)))
                    return Conflict(new { message = "Idempotency key is already used for a different request." });
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

            return CreatedAtAction(nameof(GetById), new { id = result.Redemption.Id }, result);
        }

        [HttpPost("transactions/{id:long}/reversals")]
        public async Task<ActionResult<VoucherRedeemResultResponseDto>> CreateReversal(long id, [FromBody] VoucherRedemptionReverseDto request)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var redemption = await _context.VoucherRedemptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (redemption == null || (branchId.HasValue && redemption.BranchId != branchId.Value)) return NotFound();
            if (request.ApprovalRequestId.HasValue)
            {
                var approval = await _context.VoucherApprovalRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.ApprovalRequestId.Value);
                if (approval == null || approval.RedemptionId != id || !approval.ApproverId.HasValue || approval.ApproverId.Value <= 0 ||
                    !string.Equals(approval.Status, "APPROVED", System.StringComparison.OrdinalIgnoreCase) ||
                    (branchId.HasValue && approval.BranchId != branchId.Value))
                    return BadRequest(new { message = "Approval request is invalid for this redemption." });
                request.ApprovedBy = approval.ApproverId;
            }
            else request.ApprovedBy = null;
            var result = await _service.ReverseAsync(id, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        private async Task<IActionResult?> ValidateRequestScopeAsync(VoucherRedemptionRequestDto request)
        {
            if (!this.TryResolveBranchScope(request.BranchId, out var branchId, out var denied)) return denied;
            var actorId = this.GetCurrentUserId();
            if (!actorId.HasValue || actorId.Value <= 0) return Unauthorized();
            request.AppliedBy = actorId.Value;
            if (branchId.HasValue) request.BranchId = branchId.Value;
            if (request.ServiceOrderId.HasValue && request.RetailOrderId.HasValue)
                return BadRequest(new { message = "Không thể áp dụng đồng thời cho hai loại đơn." });
            if (request.ServiceOrderId.HasValue)
            {
                var order = await _context.ServiceOrders.AsNoTracking().Where(x => x.Id == request.ServiceOrderId.Value)
                    .Select(x => new { x.ExecuteBranchId, x.CustomerId, x.VehicleId }).FirstOrDefaultAsync();
                if (order == null || (branchId.HasValue && order.ExecuteBranchId != branchId.Value)) return Forbid();
                if ((request.CustomerId.HasValue && request.CustomerId.Value != order.CustomerId) ||
                    (request.VehicleId.HasValue && request.VehicleId.Value != order.VehicleId))
                    return BadRequest(new { message = "Khách hàng hoặc xe không khớp với lệnh sửa chữa." });
            }
            if (request.RetailOrderId.HasValue)
            {
                var order = await _context.RetailOrders.AsNoTracking().Where(x => x.Id == request.RetailOrderId.Value)
                    .Select(x => new { x.BranchId, x.CustomerId }).FirstOrDefaultAsync();
                if (order == null || (branchId.HasValue && order.BranchId != branchId.Value)) return Forbid();
                if (request.CustomerId.HasValue && order.CustomerId.HasValue && request.CustomerId != order.CustomerId)
                    return BadRequest(new { message = "Khách hàng không khớp với đơn bán lẻ." });
            }
            if (request.InvoiceId.HasValue)
            {
                var invoice = await _context.Invoices.AsNoTracking().Where(x => x.Id == request.InvoiceId.Value)
                    .Select(x => new { x.ServiceOrderId, x.RetailOrderId, x.VehicleId,
                        ServiceBranch = x.ServiceOrder != null ? (long?)x.ServiceOrder.ExecuteBranchId : null,
                        RetailBranch = x.RetailOrder != null ? (long?)x.RetailOrder.BranchId : null,
                        ServiceCustomer = x.ServiceOrder != null ? (long?)x.ServiceOrder.CustomerId : null,
                        RetailCustomer = x.RetailOrder != null ? x.RetailOrder.CustomerId : null,
                        ServiceVehicle = x.ServiceOrder != null ? (long?)x.ServiceOrder.VehicleId : null }).FirstOrDefaultAsync();
                if (invoice == null || (branchId.HasValue &&
                    ((!invoice.ServiceOrderId.HasValue && !invoice.RetailOrderId.HasValue) ||
                     (invoice.ServiceOrderId.HasValue && invoice.ServiceBranch != branchId) ||
                     (invoice.RetailOrderId.HasValue && invoice.RetailBranch != branchId)))) return Forbid();
                if ((request.ServiceOrderId.HasValue && request.ServiceOrderId != invoice.ServiceOrderId) ||
                    (request.RetailOrderId.HasValue && request.RetailOrderId != invoice.RetailOrderId) ||
                    (request.CustomerId.HasValue && invoice.ServiceCustomer.HasValue && request.CustomerId != invoice.ServiceCustomer) ||
                    (request.CustomerId.HasValue && invoice.RetailCustomer.HasValue && request.CustomerId != invoice.RetailCustomer) ||
                    (request.VehicleId.HasValue && invoice.VehicleId.HasValue && request.VehicleId != invoice.VehicleId) ||
                    (request.VehicleId.HasValue && invoice.ServiceVehicle.HasValue && request.VehicleId != invoice.ServiceVehicle))
                    return BadRequest(new { message = "Hóa đơn không khớp với đơn hoặc xe đã chọn." });
            }
            return null;
        }
    }
}

