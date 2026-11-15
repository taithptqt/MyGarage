using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Application.Payments;

public sealed class PaymentLifecycleCommandService : IPaymentLifecycleCommandService
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentConfirmationService _paymentConfirmationService;
    private readonly IServiceOrderService _serviceOrderService;
    private readonly IPaymentQueryService _paymentQueryService;

    public PaymentLifecycleCommandService(
        IPaymentService paymentService,
        IPaymentConfirmationService paymentConfirmationService,
        IServiceOrderService serviceOrderService,
        IPaymentQueryService paymentQueryService)
    {
        _paymentService = paymentService;
        _paymentConfirmationService = paymentConfirmationService;
        _serviceOrderService = serviceOrderService;
        _paymentQueryService = paymentQueryService;
    }

    public async Task<PaymentCommandResult<PaymentResponseDto>> CreateAsync(
        PaymentCreateDto itemDto,
        long actorUserId,
        CancellationToken ct = default)
    {
        try
        {
            var serviceOrderId = await ResolveServiceOrderIdAsync(itemDto?.ServiceOrderId, itemDto?.InvoiceId);
            var lockError = await ValidatePaymentMutationAsync(serviceOrderId, null);
            if (!string.IsNullOrWhiteSpace(lockError))
            {
                return PaymentCommandResult<PaymentResponseDto>.BadRequest(lockError);
            }

            var entity = _paymentService.MapToEntity(itemDto!);
            entity.Status = nameof(PaymentStatusCode.PENDING);
            entity.UpdatedBy = actorUserId;
            entity.UpdatedAt = VietnamTime.Now;

            var createdItem = await _paymentService.AddAsync(entity);
            if (createdItem != null && string.Equals(createdItem.Status, nameof(PaymentStatusCode.COMPLETED), StringComparison.OrdinalIgnoreCase))
            {
                await _paymentConfirmationService.ApplyOutcomeAsync(createdItem, createdItem.Amount, createdItem.Status!);
            }

            if (createdItem == null)
            {
                return PaymentCommandResult<PaymentResponseDto>.BadRequest("Không thể tạo payment.");
            }

            return PaymentCommandResult<PaymentResponseDto>.Ok(_paymentService.MapToDto(createdItem));
        }
        catch (InvalidOperationException ex)
        {
            return PaymentCommandResult<PaymentResponseDto>.BadRequest(ex.Message);
        }
    }

    public async Task<PaymentCommandResult<PaymentResponseDto>> UpdateAsync(
        long id,
        PaymentUpdateDto itemDto,
        long actorUserId,
        CancellationToken ct = default)
    {
        try
        {
            var existingItem = await _paymentService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return PaymentCommandResult<PaymentResponseDto>.NotFound();
            }

            var serviceOrderId = await ResolveServiceOrderIdAsync(
                itemDto?.ServiceOrderId ?? existingItem.ServiceOrderId,
                itemDto?.InvoiceId ?? existingItem.InvoiceId);
            var lockError = await ValidatePaymentMutationAsync(serviceOrderId, existingItem);
            if (!string.IsNullOrWhiteSpace(lockError))
            {
                return PaymentCommandResult<PaymentResponseDto>.BadRequest(lockError);
            }

            _paymentService.MapUpdate(itemDto!, existingItem);
            existingItem.UpdatedBy = actorUserId;
            existingItem.UpdatedAt = VietnamTime.Now;

            var updatedItem = await _paymentService.UpdateAsync(id, existingItem);
            if (updatedItem != null && string.Equals(updatedItem.Status, nameof(PaymentStatusCode.COMPLETED), StringComparison.OrdinalIgnoreCase))
            {
                await _paymentConfirmationService.ApplyOutcomeAsync(updatedItem, updatedItem.Amount, updatedItem.Status!);
            }

            if (updatedItem == null)
            {
                return PaymentCommandResult<PaymentResponseDto>.NotFound();
            }

            return PaymentCommandResult<PaymentResponseDto>.Ok(_paymentService.MapToDto(updatedItem));
        }
        catch (InvalidOperationException ex)
        {
            return PaymentCommandResult<PaymentResponseDto>.BadRequest(ex.Message);
        }
    }

    public async Task<PaymentCommandResult> DeleteAsync(
        long id,
        CancellationToken ct = default)
    {
        try
        {
            var existingItem = await _paymentService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return PaymentCommandResult.NotFound();
            }

            var serviceOrderId = await ResolveServiceOrderIdAsync(existingItem.ServiceOrderId, existingItem.InvoiceId);
            var lockError = await ValidatePaymentMutationAsync(serviceOrderId, existingItem);
            if (!string.IsNullOrWhiteSpace(lockError))
            {
                return PaymentCommandResult.BadRequest(lockError);
            }

            var result = await _paymentService.DeleteAsync(id);
            if (!result)
            {
                return PaymentCommandResult.NotFound();
            }

            return PaymentCommandResult.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return PaymentCommandResult.BadRequest(ex.Message);
        }
    }

    public async Task<PaymentCommandResult<PaymentTransitionResult>> TransitionStatusAsync(
        long id,
        string? targetStatus,
        decimal? amount,
        string? note,
        long? actorUserId,
        long? scopedBranchId,
        CancellationToken ct = default)
    {
        if (!PaymentLifecycleRules.TryNormalizeTransitionTarget(targetStatus, out var normalizedTarget))
        {
            return PaymentCommandResult<PaymentTransitionResult>.BadRequest("Trạng thái đích phải là COMPLETED, FAILED hoặc CANCELED.");
        }

        var existingItem = await _paymentService.GetByIdAsync(id);
        if (existingItem == null)
        {
            return PaymentCommandResult<PaymentTransitionResult>.NotFound("Không tìm thấy giao dịch thanh toán.");
        }

        var serviceOrderId = await ResolveServiceOrderIdAsync(existingItem.ServiceOrderId, existingItem.InvoiceId);
        if (scopedBranchId.HasValue && serviceOrderId.HasValue)
        {
            var orderBranchId = await _paymentQueryService.GetServiceOrderExecuteBranchIdAsync(serviceOrderId.Value);
            if (orderBranchId.HasValue && orderBranchId.Value != scopedBranchId.Value)
            {
                return PaymentCommandResult<PaymentTransitionResult>.Forbidden();
            }
        }

        var lockError = await ValidatePaymentMutationAsync(serviceOrderId, existingItem);
        if (!string.IsNullOrWhiteSpace(lockError))
        {
            return PaymentCommandResult<PaymentTransitionResult>.BadRequest(lockError);
        }

        var effectiveAmount = amount > 0m ? amount.Value : existingItem.Amount;

        try
        {
            existingItem.Status = normalizedTarget;
            existingItem.UpdatedBy = actorUserId;
            existingItem.UpdatedAt = VietnamTime.Now;
            await _paymentService.UpdateAsync(id, existingItem);

            await _paymentConfirmationService.ApplyOutcomeAsync(existingItem, effectiveAmount, normalizedTarget);
            var updated = await _paymentService.GetByIdAsync(id) ?? existingItem;
            var dto = _paymentService.MapToDto(updated);
            return PaymentCommandResult<PaymentTransitionResult>.Ok(new PaymentTransitionResult(true, dto));
        }
        catch (InvalidOperationException ex)
        {
            return PaymentCommandResult<PaymentTransitionResult>.BadRequest(ex.Message);
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
            var fromInvoice = await _paymentQueryService.GetInvoiceServiceOrderIdAsync(invoiceId.Value);
            if (fromInvoice.HasValue && fromInvoice.Value > 0)
            {
                return fromInvoice.Value;
            }
        }

        return null;
    }

    private async Task<string?> ValidatePaymentMutationAsync(long? serviceOrderId, Payment? existingPayment)
    {
        if (existingPayment != null && PaymentLifecycleRules.IsImmutableStatus(existingPayment.Status))
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
}
