using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;

namespace MyGarage.Controllers
{
    [Authorize]
    [Route("api/payments")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IPaymentConfirmationService _paymentConfirmationService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public PaymentController(
            IPaymentService paymentService,
            IPaymentConfirmationService paymentConfirmationService,
            IServiceOrderService serviceOrderService,
            IGarageContext context,
            IMapper mapper)
        {
            _paymentService = paymentService;
            _paymentConfirmationService = paymentConfirmationService;
            _serviceOrderService = serviceOrderService;
            _context = context;
            _mapper = mapper;
        }

        [Authorize(Roles = "Staff")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentResponseDto>>> GetAll()
        {
            var items = await _paymentService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<PaymentResponseDto>>(items);
            return Ok(dtos);
        }

        [Authorize]
        [HttpGet("mine")]
        public async Task<ActionResult<IEnumerable<PaymentResponseDto>>> GetMine()
        {
            var customerId = this.GetCurrentUserId();
            if (customerId == null)
            {
                return Unauthorized();
            }

            var items = await _paymentService.GetByCustomerPayerAsync(customerId.Value);
            var dtos = _mapper.Map<IEnumerable<PaymentResponseDto>>(items);
            return Ok(dtos);
        }

        [Authorize(Roles = "Staff")]
        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentResponseDto>> GetById(long id)
        {
            var item = await _paymentService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<PaymentResponseDto>(item);
            return Ok(dto);
        }

        [Authorize]
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
                return NotFound(new { message = "Payment not found." });
            }

            var isOwnedByCustomer =
                item.PayerId.HasValue &&
                item.PayerId.Value == customerId.Value &&
                string.Equals(item.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase);

            if (!isOwnedByCustomer)
            {
                return NotFound(new { message = "Payment not found." });
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
            try
            {
                var actorUserId = this.GetCurrentUserId();
                if (actorUserId == null)
                {
                    return Unauthorized();
                }

                var serviceOrderId = await ResolveServiceOrderIdAsync(itemDto?.ServiceOrderId, itemDto?.InvoiceId);
                var lockError = await ValidatePaymentMutationAsync(serviceOrderId, null);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                var entity = _mapper.Map<Payment>(itemDto);
                entity.Status = nameof(PaymentStatusCode.PENDING);
                entity.UpdatedBy = actorUserId.Value;
                entity.UpdatedAt = VietnamTime.Now;
                var createdItem = await _paymentService.AddAsync(entity);
                if (createdItem != null && string.Equals(createdItem.Status, nameof(PaymentStatusCode.COMPLETED), StringComparison.OrdinalIgnoreCase))
                {
                    await _paymentConfirmationService.ApplyOutcomeAsync(createdItem, createdItem.Amount, createdItem.Status!);
                }
                if (createdItem == null)
                {
                    return BadRequest(new { message = "Không thể tạo payment." });
                }
                var responseDto = _mapper.Map<PaymentResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentResponseDto>> Update(long id, [FromBody] PaymentUpdateDto itemDto)
        {
            try
            {
                var actorUserId = this.GetCurrentUserId();
                if (actorUserId == null)
                {
                    return Unauthorized();
                }

                var existingItem = await _paymentService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var serviceOrderId = await ResolveServiceOrderIdAsync(
                    itemDto?.ServiceOrderId ?? existingItem.ServiceOrderId,
                    itemDto?.InvoiceId ?? existingItem.InvoiceId);
                var lockError = await ValidatePaymentMutationAsync(serviceOrderId, existingItem);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                _mapper.Map(itemDto, existingItem);
                existingItem.UpdatedBy = actorUserId.Value;
                existingItem.UpdatedAt = VietnamTime.Now;
                var updatedItem = await _paymentService.UpdateAsync(id, existingItem);
                if (updatedItem != null && string.Equals(updatedItem.Status, nameof(PaymentStatusCode.COMPLETED), StringComparison.OrdinalIgnoreCase))
                {
                    await _paymentConfirmationService.ApplyOutcomeAsync(updatedItem, updatedItem.Amount, updatedItem.Status!);
                }
                
                if (updatedItem == null)
                {
                    return NotFound();
                }
                var responseDto = _mapper.Map<PaymentResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var existingItem = await _paymentService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var serviceOrderId = await ResolveServiceOrderIdAsync(existingItem.ServiceOrderId, existingItem.InvoiceId);
                var lockError = await ValidatePaymentMutationAsync(serviceOrderId, existingItem);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                var result = await _paymentService.DeleteAsync(id);
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<long?> ResolveServiceOrderIdAsync(long? serviceOrderId, long? invoiceId)
        {
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                return serviceOrderId.Value;
            }

            if (invoiceId.HasValue && invoiceId.Value > 0)
            {
                var fromInvoice = await _context.Invoices
                    .AsNoTracking()
                    .Where(x => x.Id == invoiceId.Value)
                    .Select(x => x.ServiceOrderId)
                    .FirstOrDefaultAsync();
                if (fromInvoice.HasValue && fromInvoice.Value > 0)
                {
                    return fromInvoice.Value;
                }
            }

            return null;
        }

        private async Task<string?> ValidatePaymentMutationAsync(long? serviceOrderId, Payment? existingPayment)
        {
            if (existingPayment != null && IsImmutablePaymentStatus(existingPayment.Status))
            {
                return "Không thể chỉnh sửa thanh toán đã hoàn tất/hủy/thất bại.";
            }

            if (!serviceOrderId.HasValue || serviceOrderId.Value <= 0)
            {
                return null;
            }

            var capability = await _serviceOrderService.GetEditCapabilityAsync(serviceOrderId.Value);
            if (capability == null)
            {
                return "Không tìm thấy service order để xử lý thanh toán.";
            }

            if (capability.IsClosedLocked)
            {
                return "Không thể chỉnh sửa thanh toán vì service order đã đóng.";
            }

            return null;
        }

        private static bool IsImmutablePaymentStatus(string? status)
        {
            var normalized = string.IsNullOrWhiteSpace(status)
                ? string.Empty
                : status.Trim().ToUpperInvariant();
            return normalized == nameof(PaymentStatusCode.COMPLETED)
                || normalized == nameof(PaymentStatusCode.CANCELED)
                || normalized == nameof(PaymentStatusCode.FAILED);
        }
    }
}






