using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Payments;

namespace MyGarage.Controllers
{
    [Authorize]
    [Route("api/v1/payments")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        public sealed record PaymentTransitionStatusDto(string? Status, decimal? Amount, string? Note);

        private readonly IPaymentService _paymentService;
        private readonly IPaymentLifecycleCommandService _paymentLifecycleCommandService;
        private readonly CustomerPaymentsQuery _customerPaymentsQuery;

        public PaymentController(
            IPaymentService paymentService,
            IPaymentLifecycleCommandService paymentLifecycleCommandService,
            CustomerPaymentsQuery customerPaymentsQuery)
        {
            _paymentService = paymentService;
            _paymentLifecycleCommandService = paymentLifecycleCommandService;
            _customerPaymentsQuery = customerPaymentsQuery;
        }

        [Authorize(Roles = "Staff")]
        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<PaymentResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _paymentService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                paged => _paymentService.MapToDtos(paged));
            return Ok(envelope);
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("mine")]
        public async Task<ActionResult<PagedEnvelope<CustomerPaymentRow>>> GetMine(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            CancellationToken cancellationToken = default)
        {
            var customerId = this.GetCurrentUserId();
            if (customerId == null)
            {
                return Unauthorized();
            }

            var result = await _customerPaymentsQuery.ExecuteAsync(customerId.Value, page, pageSize, cancellationToken);
            return Ok(PagedEnvelope<CustomerPaymentRow>.Create(result.Items, result.Total, result.Page, result.PageSize));
        }

        [Authorize(Roles = "Staff")]
        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentResponseDto>> GetById(long id)
        {
            var dto = await _paymentService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("{id:long}/status")]
        public async Task<ActionResult<object>> GetStatus(long id)
        {
            var customerId = this.GetCurrentUserId();
            if (customerId == null)
            {
                return Unauthorized();
            }

            var item = await _paymentService.GetByIdAsync(id);
            if (item == null)
            {
                return this.NotFoundProblem("Payment not found.");
            }

            var isOwnedByCustomer =
                item.PayerId.HasValue &&
                item.PayerId.Value == customerId.Value &&
                string.Equals(item.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase);

            if (!isOwnedByCustomer)
            {
                return this.NotFoundProblem("Payment not found.");
            }

            var normalizedStatus = string.IsNullOrWhiteSpace(item.Status)
                ? "PENDING"
                : item.Status.Trim().ToUpperInvariant();

            return Ok(new
            {
                id = item.Id,
                status = normalizedStatus,
                amount = item.Amount,
                transactionCode = item.TransactionCode,
                updatedAt = item.UpdatedAt
            });
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<PaymentResponseDto>> Create([FromBody] PaymentCreateDto itemDto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (actorUserId == null)
            {
                return Unauthorized();
            }

            var result = await _paymentLifecycleCommandService.CreateAsync(itemDto, actorUserId.Value, HttpContext.RequestAborted);
            return result.Status switch
            {
                PaymentCommandStatus.Success => CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data),
                PaymentCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Không thể tạo payment."),
                PaymentCommandStatus.NotFound => string.IsNullOrWhiteSpace(result.ErrorMessage) ? NotFound() : this.NotFoundProblem(result.ErrorMessage),
                PaymentCommandStatus.Forbidden => Forbid(),
                _ => this.BadRequestProblem("Không thể tạo payment.")
            };
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentResponseDto>> Update(long id, [FromBody] PaymentUpdateDto itemDto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (actorUserId == null)
            {
                return Unauthorized();
            }

            var result = await _paymentLifecycleCommandService.UpdateAsync(id, itemDto, actorUserId.Value, HttpContext.RequestAborted);
            return result.Status switch
            {
                PaymentCommandStatus.Success => Ok(result.Data),
                PaymentCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Không thể cập nhật payment."),
                PaymentCommandStatus.NotFound => string.IsNullOrWhiteSpace(result.ErrorMessage) ? NotFound() : this.NotFoundProblem(result.ErrorMessage),
                PaymentCommandStatus.Forbidden => Forbid(),
                _ => this.BadRequestProblem("Không thể cập nhật payment.")
            };
        }

        [Authorize(Roles = "Staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _paymentLifecycleCommandService.DeleteAsync(id, HttpContext.RequestAborted);
            return result.Status switch
            {
                PaymentCommandStatus.Success => NoContent(),
                PaymentCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Không thể xóa payment."),
                PaymentCommandStatus.NotFound => string.IsNullOrWhiteSpace(result.ErrorMessage) ? NotFound() : this.NotFoundProblem(result.ErrorMessage),
                PaymentCommandStatus.Forbidden => Forbid(),
                _ => this.BadRequestProblem("Không thể xóa payment.")
            };
        }

        [Authorize(Roles = "Staff")]
        [HttpPost("{id:long}/status-transitions")]
        public async Task<IActionResult> TransitionStatus(long id, [FromBody] PaymentTransitionStatusDto? payload)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "ACCOUNTANT", "FINANCE", "KE_TOAN", "ADVISOR", "RECEPTIONIST"))
            {
                return Forbid();
            }

            var actorUserId = this.GetCurrentUserId();
            var result = await _paymentLifecycleCommandService.TransitionStatusAsync(
                id,
                payload?.Status,
                payload?.Amount,
                payload?.Note,
                actorUserId,
                scopedBranchId,
                HttpContext.RequestAborted);

            return result.Status switch
            {
                PaymentCommandStatus.Success => Ok(new { confirmed = result.Data!.Confirmed, payment = result.Data.Payment }),
                PaymentCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Chuyển trạng thái thanh toán không hợp lệ."),
                PaymentCommandStatus.NotFound => string.IsNullOrWhiteSpace(result.ErrorMessage) ? NotFound() : this.NotFoundProblem(result.ErrorMessage),
                PaymentCommandStatus.Forbidden => Forbid(),
                _ => this.BadRequestProblem("Chuyển trạng thái thanh toán không hợp lệ.")
            };
        }
    }
}






