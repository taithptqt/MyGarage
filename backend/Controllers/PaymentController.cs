using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<Payment> query = _context.Payments
                .AsNoTracking();

            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(x =>
                    (x.ServiceOrder != null && (x.ServiceOrder.ExecuteBranchId == branchId || x.ServiceOrder.OriginBranchId == branchId))
                    || (x.RetailOrder != null && x.RetailOrder.BranchId == branchId)
                    || (x.Invoice != null && (
                        (x.Invoice.ServiceOrder != null && (x.Invoice.ServiceOrder.ExecuteBranchId == branchId || x.Invoice.ServiceOrder.OriginBranchId == branchId))
                        || (x.Invoice.RetailOrder != null && x.Invoice.RetailOrder.BranchId == branchId)
                    )));
            }

            var items = await query.ToListAsync();
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
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<Payment> query = _context.Payments
                .AsNoTracking()
                .Where(x => x.Id == id);

            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(x =>
                    (x.ServiceOrder != null && (x.ServiceOrder.ExecuteBranchId == branchId || x.ServiceOrder.OriginBranchId == branchId))
                    || (x.RetailOrder != null && x.RetailOrder.BranchId == branchId)
                    || (x.Invoice != null && (
                        (x.Invoice.ServiceOrder != null && (x.Invoice.ServiceOrder.ExecuteBranchId == branchId || x.Invoice.ServiceOrder.OriginBranchId == branchId))
                        || (x.Invoice.RetailOrder != null && x.Invoice.RetailOrder.BranchId == branchId)
                    )));
            }

            var item = await query.FirstOrDefaultAsync();
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
                if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

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

                if (scopedBranchId.HasValue)
                {
                    var inBranch = await IsPaymentSourceInBranchAsync(serviceOrderId, itemDto?.RetailOrderId, itemDto?.InvoiceId, scopedBranchId.Value);
                    if (!inBranch) return NotFound();
                }

                var amount = itemDto?.Amount ?? 0m;
                if (amount <= 0m)
                {
                    return BadRequest(new { message = "Amount must be greater than 0." });
                }

                var duplicateCreateResult = await TryHandleDuplicateCreateAsync(itemDto, scopedBranchId);
                if (duplicateCreateResult != null)
                {
                    return duplicateCreateResult;
                }

                var overPaymentError = await ValidateOverPaymentAsync(
                    itemDto?.InvoiceId,
                    itemDto?.ServiceOrderId,
                    itemDto?.RetailOrderId,
                    amount,
                    null);
                if (!string.IsNullOrWhiteSpace(overPaymentError))
                {
                    return BadRequest(new { message = overPaymentError });
                }

                var entity = _mapper.Map<Payment>(itemDto);
                entity.Amount = amount;
                entity.TransactionCode = NormalizeTransactionCode(itemDto?.TransactionCode);
                entity.Status = nameof(PaymentStatusCode.PENDING);
                if (!entity.CashierId.HasValue || entity.CashierId.Value <= 0)
                {
                    entity.CashierId = actorUserId.Value;
                }
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
                if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

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
                if (scopedBranchId.HasValue)
                {
                    var existingInBranch = await IsPaymentInBranchAsync(existingItem.Id, scopedBranchId.Value);
                    if (!existingInBranch) return NotFound();
                }

                var serviceOrderId = await ResolveServiceOrderIdAsync(
                    itemDto?.ServiceOrderId ?? existingItem.ServiceOrderId,
                    itemDto?.InvoiceId ?? existingItem.InvoiceId);
                var lockError = await ValidatePaymentMutationAsync(serviceOrderId, existingItem);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                if (scopedBranchId.HasValue)
                {
                    var targetInBranch = await IsPaymentSourceInBranchAsync(
                        serviceOrderId,
                        itemDto?.RetailOrderId ?? existingItem.RetailOrderId,
                        itemDto?.InvoiceId ?? existingItem.InvoiceId,
                        scopedBranchId.Value);
                    if (!targetInBranch) return NotFound();
                }

                var amount = itemDto?.Amount ?? existingItem.Amount;
                if (amount <= 0m)
                {
                    return BadRequest(new { message = "Amount must be greater than 0." });
                }

                var duplicateUpdateError = await ValidateDuplicateTransactionCodeAsync(
                    itemDto?.TransactionCode,
                    itemDto?.InvoiceId ?? existingItem.InvoiceId,
                    itemDto?.ServiceOrderId ?? existingItem.ServiceOrderId,
                    itemDto?.RetailOrderId ?? existingItem.RetailOrderId,
                    itemDto?.MethodId ?? existingItem.MethodId,
                    amount,
                    existingItem.Id);
                if (!string.IsNullOrWhiteSpace(duplicateUpdateError))
                {
                    return Conflict(new { message = duplicateUpdateError });
                }

                var overPaymentError = await ValidateOverPaymentAsync(
                    itemDto?.InvoiceId ?? existingItem.InvoiceId,
                    itemDto?.ServiceOrderId ?? existingItem.ServiceOrderId,
                    itemDto?.RetailOrderId ?? existingItem.RetailOrderId,
                    amount,
                    existingItem.Id);
                if (!string.IsNullOrWhiteSpace(overPaymentError))
                {
                    return BadRequest(new { message = overPaymentError });
                }

                _mapper.Map(itemDto, existingItem);
                existingItem.Amount = amount;
                existingItem.TransactionCode = NormalizeTransactionCode(itemDto?.TransactionCode) ?? existingItem.TransactionCode;
                if (!existingItem.CashierId.HasValue || existingItem.CashierId.Value <= 0)
                {
                    existingItem.CashierId = actorUserId.Value;
                }
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
                if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

                var existingItem = await _paymentService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }
                if (scopedBranchId.HasValue)
                {
                    var existingInBranch = await IsPaymentInBranchAsync(existingItem.Id, scopedBranchId.Value);
                    if (!existingInBranch) return NotFound();
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

        private bool TryResolveAccountingBranchScope(out long? branchId, out ActionResult deniedResult)
        {
            branchId = null;
            deniedResult = null!;

            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                deniedResult = Unauthorized();
                return false;
            }

            if (this.IsCurrentAccountingStaff())
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

        private async Task<bool> IsPaymentInBranchAsync(long paymentId, long branchId)
        {
            if (paymentId <= 0) return false;

            return await _context.Payments
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == paymentId
                    && (
                        (x.ServiceOrder != null && (x.ServiceOrder.ExecuteBranchId == branchId || x.ServiceOrder.OriginBranchId == branchId))
                        || (x.RetailOrder != null && x.RetailOrder.BranchId == branchId)
                        || (x.Invoice != null && (
                            (x.Invoice.ServiceOrder != null && (x.Invoice.ServiceOrder.ExecuteBranchId == branchId || x.Invoice.ServiceOrder.OriginBranchId == branchId))
                            || (x.Invoice.RetailOrder != null && x.Invoice.RetailOrder.BranchId == branchId)
                        ))
                    ));
        }

        private async Task<bool> IsPaymentSourceInBranchAsync(long? serviceOrderId, long? retailOrderId, long? invoiceId, long branchId)
        {
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                var serviceOrderInBranch = await _context.ServiceOrders
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == serviceOrderId.Value && (x.ExecuteBranchId == branchId || x.OriginBranchId == branchId));
                if (!serviceOrderInBranch) return false;
            }

            if (retailOrderId.HasValue && retailOrderId.Value > 0)
            {
                var retailOrderInBranch = await _context.RetailOrders
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == retailOrderId.Value && x.BranchId == branchId);
                if (!retailOrderInBranch) return false;
            }

            if (invoiceId.HasValue && invoiceId.Value > 0)
            {
                var invoiceInBranch = await _context.Invoices
                    .AsNoTracking()
                    .Include(x => x.ServiceOrder)
                    .Include(x => x.RetailOrder)
                    .AnyAsync(x =>
                        x.Id == invoiceId.Value
                        && (
                            (x.ServiceOrder != null && (x.ServiceOrder.ExecuteBranchId == branchId || x.ServiceOrder.OriginBranchId == branchId))
                            || (x.RetailOrder != null && x.RetailOrder.BranchId == branchId)
                        ));
                if (!invoiceInBranch) return false;
            }

            return true;
        }

        private async Task<ActionResult?> TryHandleDuplicateCreateAsync(PaymentCreateDto? itemDto, long? scopedBranchId)
        {
            if (itemDto == null)
            {
                return null;
            }
            var requestDto = itemDto;

            var txCode = NormalizeTransactionCode(requestDto.TransactionCode);
            if (string.IsNullOrWhiteSpace(txCode))
            {
                return null;
            }

            IQueryable<Payment> query = _context.Payments.AsNoTracking().Where(x => x.TransactionCode == txCode);
            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(x =>
                    (x.ServiceOrder != null && (x.ServiceOrder.ExecuteBranchId == branchId || x.ServiceOrder.OriginBranchId == branchId))
                    || (x.RetailOrder != null && x.RetailOrder.BranchId == branchId)
                    || (x.Invoice != null && (
                        (x.Invoice.ServiceOrder != null && (x.Invoice.ServiceOrder.ExecuteBranchId == branchId || x.Invoice.ServiceOrder.OriginBranchId == branchId))
                        || (x.Invoice.RetailOrder != null && x.Invoice.RetailOrder.BranchId == branchId)
                    )));
            }

            var duplicate = await query.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
            if (duplicate == null)
            {
                return null;
            }

            var sameSource =
                duplicate.InvoiceId == requestDto.InvoiceId &&
                duplicate.ServiceOrderId == requestDto.ServiceOrderId &&
                duplicate.RetailOrderId == requestDto.RetailOrderId &&
                duplicate.MethodId == requestDto.MethodId &&
                Math.Abs(duplicate.Amount - (requestDto.Amount ?? 0m)) <= 0.005m;
            if (!sameSource)
            {
                return Conflict(new { message = "Duplicate transactionCode detected with different payment payload." });
            }

            var response = _mapper.Map<PaymentResponseDto>(duplicate);
            return Ok(response);
        }

        private async Task<string?> ValidateDuplicateTransactionCodeAsync(
            string? transactionCode,
            long? invoiceId,
            long? serviceOrderId,
            long? retailOrderId,
            long methodId,
            decimal amount,
            long? excludePaymentId)
        {
            var normalized = NormalizeTransactionCode(transactionCode);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var duplicate = await _context.Payments
                .AsNoTracking()
                .Where(x => x.TransactionCode == normalized && (!excludePaymentId.HasValue || x.Id != excludePaymentId.Value))
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
            if (duplicate == null)
            {
                return null;
            }

            var sameSource =
                duplicate.InvoiceId == invoiceId &&
                duplicate.ServiceOrderId == serviceOrderId &&
                duplicate.RetailOrderId == retailOrderId &&
                duplicate.MethodId == methodId &&
                Math.Abs(duplicate.Amount - amount) <= 0.005m;
            if (sameSource)
            {
                return "Duplicate transactionCode: this payment was already submitted.";
            }

            return "Duplicate transactionCode detected with different payment payload.";
        }

        private async Task<string?> ValidateOverPaymentAsync(
            long? invoiceId,
            long? serviceOrderId,
            long? retailOrderId,
            decimal amount,
            long? excludePaymentId)
        {
            if (amount <= 0m)
            {
                return "Amount must be greater than 0.";
            }

            decimal totalAmount;
            decimal paidAmount;
            if (invoiceId.HasValue && invoiceId.Value > 0)
            {
                var invoice = await _context.Invoices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == invoiceId.Value);
                if (invoice == null)
                {
                    return "Invoice not found for payment.";
                }

                totalAmount = invoice.TotalAmount ?? 0m;
                paidAmount = await SumEffectivePaymentsAsync(invoiceId: invoiceId.Value, excludePaymentId: excludePaymentId);
            }
            else if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                var serviceOrder = await _context.ServiceOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == serviceOrderId.Value);
                if (serviceOrder == null)
                {
                    return "Service order not found for payment.";
                }

                totalAmount = serviceOrder.FinalAmount ?? 0m;
                paidAmount = await SumEffectivePaymentsAsync(serviceOrderId: serviceOrderId.Value, excludePaymentId: excludePaymentId);
            }
            else if (retailOrderId.HasValue && retailOrderId.Value > 0)
            {
                var retailOrder = await _context.RetailOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == retailOrderId.Value);
                if (retailOrder == null)
                {
                    return "Retail order not found for payment.";
                }

                totalAmount = retailOrder.FinalAmount;
                paidAmount = await SumEffectivePaymentsAsync(retailOrderId: retailOrderId.Value, excludePaymentId: excludePaymentId);
            }
            else
            {
                return null;
            }

            var remaining = Math.Max(0m, totalAmount - paidAmount);
            if (amount > remaining + 0.005m)
            {
                return $"Payment amount exceeds outstanding balance. Remaining balance is {remaining:0.##}.";
            }

            return null;
        }

        private async Task<decimal> SumEffectivePaymentsAsync(
            long? invoiceId = null,
            long? serviceOrderId = null,
            long? retailOrderId = null,
            long? excludePaymentId = null)
        {
            var excludedStatuses = new[]
            {
                nameof(PaymentStatusCode.CANCELED),
                nameof(PaymentStatusCode.FAILED)
            };

            IQueryable<Payment> query = _context.Payments.AsNoTracking();
            if (invoiceId.HasValue)
            {
                query = query.Where(x => x.InvoiceId == invoiceId.Value);
            }
            else if (serviceOrderId.HasValue)
            {
                query = query.Where(x => x.ServiceOrderId == serviceOrderId.Value);
            }
            else if (retailOrderId.HasValue)
            {
                query = query.Where(x => x.RetailOrderId == retailOrderId.Value);
            }

            if (excludePaymentId.HasValue)
            {
                query = query.Where(x => x.Id != excludePaymentId.Value);
            }

            query = query.Where(x =>
                x.Status == null ||
                !excludedStatuses.Contains(x.Status.ToUpper()));

            return await query.SumAsync(x => (decimal?)x.Amount) ?? 0m;
        }

        private static string? NormalizeTransactionCode(string? transactionCode)
        {
            if (string.IsNullOrWhiteSpace(transactionCode))
            {
                return null;
            }

            return transactionCode.Trim();
        }
    }
}







