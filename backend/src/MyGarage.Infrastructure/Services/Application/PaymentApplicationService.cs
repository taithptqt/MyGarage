using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Payments;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Services;

public sealed class PaymentApplicationService : IPaymentApplicationService
{
    private readonly IGarageContext _context;
    private readonly IPaymentAuthorizationService _authService;

    public PaymentApplicationService(
        IGarageContext context,
        IPaymentAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _context.Database.BeginTransactionAsync();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return null;
            }
            throw;
        }
    }

    public async Task<PaymentDto?> GetByIdAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var canView = await _authService.CanViewPaymentAsync(id, actor, ct);
        if (!canView)
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập thông tin giao dịch thanh toán này.");
        }

        var payment = await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return payment == null ? null : MapToDto(payment);
    }

    public async Task<PaymentDto> CreatePaymentAsync(PaymentCreateInput input, UserActorContext actor, CancellationToken ct = default)
    {
        long? branchId = null;
        if (input.ServiceOrderId.HasValue && input.ServiceOrderId.Value > 0)
        {
            branchId = await _context.ServiceOrders
                .AsNoTracking()
                .Where(so => so.Id == input.ServiceOrderId.Value)
                .Select(so => (long?)so.ExecuteBranchId)
                .FirstOrDefaultAsync(ct);
        }
        else if (input.RetailOrderId.HasValue && input.RetailOrderId.Value > 0)
        {
            branchId = await _context.RetailOrders
                .AsNoTracking()
                .Where(ro => ro.Id == input.RetailOrderId.Value)
                .Select(ro => (long?)ro.BranchId)
                .FirstOrDefaultAsync(ct);
        }

        var canCreate = await _authService.CanCreatePaymentAsync(branchId, actor, ct);
        if (!canCreate)
        {
            throw new UnauthorizedAccessException("Không có quyền tạo giao dịch thanh toán.");
        }

        if (input.Amount <= 0m)
        {
            throw new InvalidOperationException("Số tiền thanh toán phải lớn hơn 0.");
        }

        var normalizedDirection = PaymentLifecycleRules.NormalizeDirection(input.Direction, input.InvoiceId);
        var normalizedStatus = PaymentLifecycleRules.NormalizeStatus(input.Status);
        var now = VietnamTime.Now;

        var payment = new Payment
        {
            InvoiceId = input.InvoiceId,
            ServiceOrderId = input.ServiceOrderId,
            RetailOrderId = input.RetailOrderId,
            PayerType = string.IsNullOrWhiteSpace(input.PayerType) ? "CUSTOMER" : input.PayerType.Trim(),
            PayerId = input.PayerId ?? actor.UserId,
            MethodId = input.MethodId,
            Amount = input.Amount,
            TransactionCode = string.IsNullOrWhiteSpace(input.TransactionCode) ? null : input.TransactionCode.Trim(),
            Type = string.IsNullOrWhiteSpace(input.Type) ? "PAYMENT" : input.Type.Trim(),
            Status = normalizedStatus,
            Direction = normalizedDirection,
            CashierId = actor.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedBy = actor.UserId
        };

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync(ct);

                if (tx != null)
                {
                    await tx.CommitAsync(ct);
                }
            }
            catch
            {
                if (tx != null)
                {
                    await tx.RollbackAsync(ct);
                }
                throw;
            }
        });

        return MapToDto(payment);
    }

    public async Task<PaymentDto> TransitionStatusAsync(long id, PaymentTransitionInput input, UserActorContext actor, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Mã giao dịch không hợp lệ.");
        }

        var canTransition = await _authService.CanTransitionPaymentStatusAsync(id, actor, ct);
        if (!canTransition)
        {
            throw new UnauthorizedAccessException("Không có quyền chuyển trạng thái giao dịch thanh toán này.");
        }

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payment == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy giao dịch thanh toán #{id}.");
        }

        var (isValid, error) = PaymentLifecycleRules.ValidateTransition(payment.Status, input.Status);
        if (!isValid)
        {
            throw new InvalidOperationException(error ?? "Chuyển đổi trạng thái thanh toán không hợp lệ.");
        }

        var targetStatus = PaymentLifecycleRules.NormalizeStatus(input.Status);
        var now = VietnamTime.Now;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                payment.Status = targetStatus;
                if (input.Amount.HasValue && input.Amount.Value > 0m)
                {
                    payment.Amount = input.Amount.Value;
                }
                payment.UpdatedAt = now;
                payment.UpdatedBy = actor.UserId;

                await _context.SaveChangesAsync(ct);

                if (tx != null)
                {
                    await tx.CommitAsync(ct);
                }
            }
            catch
            {
                if (tx != null)
                {
                    await tx.RollbackAsync(ct);
                }
                throw;
            }
        });

        return MapToDto(payment);
    }

    public async Task<IReadOnlyList<PaymentDto>> GetCustomerPaymentsAsync(long customerId, UserActorContext actor, CancellationToken ct = default)
    {
        if (customerId <= 0) return Array.Empty<PaymentDto>();

        // Customer can only view their own payments unless staff/admin
        var isSelf = actor.UserId == customerId && string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase);
        var isStaffOrAdmin = actor.IsAdmin || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase);

        if (!isSelf && !isStaffOrAdmin)
        {
            throw new UnauthorizedAccessException("Không có quyền xem lịch sử thanh toán của khách hàng này.");
        }

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.PayerId == customerId && p.PayerType == "CUSTOMER")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return payments.Select(MapToDto).ToList();
    }

    private static PaymentDto MapToDto(Payment p) => new(
        p.Id,
        p.InvoiceId,
        p.ServiceOrderId,
        p.RetailOrderId,
        p.PayerType,
        p.PayerId,
        p.MethodId,
        p.Amount,
        p.TransactionCode,
        p.Type,
        p.Status,
        p.Direction,
        p.CreatedAt,
        p.UpdatedAt
    );
}
