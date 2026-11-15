// Mục đích: Cung cấp API kiểm tra điều kiện, redeem, reverse và tra cứu lịch sử sử dụng voucher.
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
    [Route("api/v1/voucher-redemptions")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class VoucherRedemptionController : ControllerBase
    {
        private readonly IVoucherRedemptionService _service;
        private readonly IVoucherRedemptionQueryService _queryService;

        public VoucherRedemptionController(IVoucherRedemptionService service, IVoucherRedemptionQueryService queryService)
        {
            _service = service;
            _queryService = queryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<VoucherRedemptionResponseDto>>> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _queryService.GetPageAsync(branchId, status, normalizedPage, normalizedSize, HttpContext.RequestAborted);
            var dtos = _service.MapToDtos(paged);
            return Ok(PagedEnvelope<VoucherRedemptionResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        // Separate opt-in page preserves the legacy array/header contract.
        [HttpGet("admin-page")]
        public async Task<IActionResult> GetAdminPage([FromQuery] string? search, [FromQuery] string? sort,
            [FromQuery] int? page, [FromQuery] int? pageSize, System.Threading.CancellationToken cancellationToken)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return denied!;
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
            var result = await _queryService.GetAdminPageAsync(branchId, search, sort, current, size, cancellationToken);
            return Ok(new { items = _service.MapToDtos(result.Items), total = result.Total, totalDiscount = result.TotalDiscount });
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VoucherRedemptionResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var dto = await _service.GetDetailedDtoByIdAsync(id);
            if (dto == null || (branchId.HasValue && dto.BranchId != branchId.Value))
            {
                return NotFound();
            }

            return Ok(dto);
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
                var existing = await _queryService.FindByIdempotencyKeyAsync(key, HttpContext.RequestAborted);
                if (existing != null &&
                    (existing.BranchId != request.BranchId || existing.AppliedBy != request.AppliedBy ||
                     existing.CustomerId != request.CustomerId || existing.VehicleId != request.VehicleId ||
                     existing.ServiceOrderId != request.ServiceOrderId || existing.RetailOrderId != request.RetailOrderId ||
                     existing.InvoiceId != request.InvoiceId || existing.PaymentMethodId != request.PaymentMethodId ||
                     (request.VoucherId.HasValue && existing.VoucherId != request.VoucherId.Value) ||
                     (!string.IsNullOrWhiteSpace(request.VoucherCode) && !string.Equals(existing.VoucherCode, request.VoucherCode.Trim(), System.StringComparison.OrdinalIgnoreCase)) ||
                     (!request.InvoiceId.HasValue && request.EligibleAmount.HasValue && existing.EligibleAmount != request.EligibleAmount.Value)))
                    return this.ConflictProblem("Idempotency key is already used for a different request.");
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
            var redemption = await _queryService.FindRedemptionByIdAsync(id, HttpContext.RequestAborted);
            if (redemption == null || (branchId.HasValue && redemption.BranchId != branchId.Value)) return NotFound();
            if (request.ApprovalRequestId.HasValue)
            {
                var approval = await _queryService.FindApprovalRequestByIdAsync(request.ApprovalRequestId.Value, HttpContext.RequestAborted);
                if (approval == null || approval.RedemptionId != id || !approval.ApproverId.HasValue || approval.ApproverId.Value <= 0 ||
                    !string.Equals(approval.Status, "APPROVED", System.StringComparison.OrdinalIgnoreCase) ||
                    (branchId.HasValue && approval.BranchId != branchId.Value))
                    return this.BadRequestProblem("Approval request is invalid for this redemption.");
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
                return this.BadRequestProblem("Không thể áp dụng đồng thời cho hai loại đơn.");
            if (request.ServiceOrderId.HasValue)
            {
                var order = await _queryService.GetServiceOrderScopeAsync(request.ServiceOrderId.Value, HttpContext.RequestAborted);
                if (order == null || (branchId.HasValue && order.ExecuteBranchId != branchId.Value)) return Forbid();
                if ((request.CustomerId.HasValue && request.CustomerId.Value != order.CustomerId) ||
                    (request.VehicleId.HasValue && request.VehicleId.Value != order.VehicleId))
                    return this.BadRequestProblem("Khách hàng hoặc xe không khớp với lệnh sửa chữa.");
            }
            if (request.RetailOrderId.HasValue)
            {
                var order = await _queryService.GetRetailOrderScopeAsync(request.RetailOrderId.Value, HttpContext.RequestAborted);
                if (order == null || (branchId.HasValue && order.BranchId != branchId.Value)) return Forbid();
                if (request.CustomerId.HasValue && order.CustomerId.HasValue && request.CustomerId != order.CustomerId)
                    return this.BadRequestProblem("Khách hàng không khớp với đơn bán lẻ.");
            }
            if (request.InvoiceId.HasValue)
            {
                var invoice = await _queryService.GetInvoiceScopeAsync(request.InvoiceId.Value, HttpContext.RequestAborted);
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
                    return this.BadRequestProblem("Hóa đơn không khớp với đơn hoặc xe đã chọn.");
            }
            return null;
        }
    }
}
