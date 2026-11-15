using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfOrderDataService(IGarageContext db) : IOrderDataService
{
    private const string RetailOrderStatusConfirmed = nameof(RetailOrderStatus.CONFIRMED);
    private const string RetailOrderStatusDraft = nameof(RetailOrderStatus.DRAFT);
    private const string PaymentStatusCompleted = nameof(PaymentStatusCode.COMPLETED);

    private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(ct);
            try
            {
                var result = await action();
                if (tx != null)
                {
                    await tx.CommitAsync(ct);
                }

                return result;
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
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await db.Database.BeginTransactionAsync(ct);
    }

    private static bool IsInflowRequiresInvoiceConstraintViolation(Exception ex)
    {
        var cursor = ex;
        while (cursor is not null)
        {
            if (cursor is SqlException sql &&
                sql.Message.Contains("CHK_Payments_InflowRequiresInvoice", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            cursor = cursor.InnerException!;
        }

        return false;
    }

    public async Task UpdateRetailOrderStatusDirectlyAsync(
        long retailOrderId,
        bool isPaid,
        CancellationToken ct = default)
    {
        var retailOrder = await db.RetailOrders.FirstOrDefaultAsync(x => x.Id == retailOrderId, ct);
        if (retailOrder is null)
        {
            return;
        }

        await ExecuteInTransactionAsync(async () =>
        {
            retailOrder.Status = isPaid ? RetailOrderStatusConfirmed : RetailOrderStatusDraft;
            retailOrder.UpdatedAt = VietnamTime.Now;
            await db.SaveChangesAsync(ct);
            return 0;
        }, ct);

        if (isPaid)
        {
            await RetailOrderStockCommitter.EnsureStockDeductedAsync(db, retailOrderId);
        }
    }

    public Task EnsureRetailOrderStockDeductedAsync(long retailOrderId, CancellationToken ct = default) =>
        RetailOrderStockCommitter.EnsureStockDeductedAsync(db, retailOrderId);

    public async Task<long> ResolvePayOsMethodIdAsync(CancellationToken ct = default)
    {
        var method = await db.PaymentMethods
            .FirstOrDefaultAsync(m => m.MethodCode == "PAYOS" && m.IsDeleted != true, ct);

        if (method is not null)
        {
            return method.Id;
        }

        var now = VietnamTime.Now;
        method = new PaymentMethod
        {
            MethodName = "PayOS",
            MethodCode = "PAYOS",
            ProviderInfo = "PayOS Gateway",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        return await ExecuteInTransactionAsync(async () =>
        {
            db.PaymentMethods.Add(method);
            await db.SaveChangesAsync(ct);
            return method.Id;
        }, ct);
    }

    public async Task<long?> ResolveInvoiceIdAsync(
        long? invoiceId,
        long? serviceOrderId,
        long? retailOrderId,
        CancellationToken ct = default)
    {
        if (invoiceId.HasValue && invoiceId.Value > 0)
        {
            return invoiceId.Value;
        }

        if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
        {
            return await db.Invoices
                .AsNoTracking()
                .Where(i => i.ServiceOrderId == serviceOrderId.Value)
                .OrderByDescending(i => i.IssuedAt ?? i.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(i => i.Id)
                .Select(i => (long?)i.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (retailOrderId.HasValue && retailOrderId.Value > 0)
        {
            return await db.Invoices
                .AsNoTracking()
                .Where(i => i.RetailOrderId == retailOrderId.Value)
                .OrderByDescending(i => i.IssuedAt ?? i.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(i => i.Id)
                .Select(i => (long?)i.Id)
                .FirstOrDefaultAsync(ct);
        }

        return null;
    }

    public async Task<Payment?> ResolvePaymentAsync(
        long? paymentRecordId,
        string? paymentLinkId,
        CancellationToken ct = default)
    {
        if (paymentRecordId.HasValue)
        {
            var byId = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentRecordId.Value, ct);
            if (byId is not null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(paymentLinkId))
        {
            return await db.Payments
                .Where(p => p.TransactionCode == paymentLinkId)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync(ct);
        }

        return null;
    }

    public Task<long?> GetRetailOrderCustomerIdAsync(long retailOrderId, CancellationToken ct = default) =>
        db.RetailOrders
            .AsNoTracking()
            .Where(r => r.Id == retailOrderId)
            .Select(r => (long?)r.CustomerId)
            .FirstOrDefaultAsync(ct);

    public Task<long?> GetServiceOrderCustomerIdAsync(long serviceOrderId, CancellationToken ct = default) =>
        db.ServiceOrders
            .AsNoTracking()
            .Where(s => s.Id == serviceOrderId)
            .Select(s => (long?)s.CustomerId)
            .FirstOrDefaultAsync(ct);

    public Task<OrderInvoiceScope?> GetInvoiceScopeAsync(long invoiceId, CancellationToken ct = default) =>
        db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new OrderInvoiceScope(
                i.ServiceOrder != null ? (long?)i.ServiceOrder.CustomerId : null,
                i.RetailOrder != null ? (long?)i.RetailOrder.CustomerId : null,
                i.ServiceOrderId,
                i.RetailOrderId))
            .FirstOrDefaultAsync(ct);

    public async Task<OrderPendingPaymentCreateResult> TryCreatePendingPaymentAsync(
        Payment payment,
        CancellationToken ct = default)
    {
        try
        {
            await ExecuteInTransactionAsync(async () =>
            {
                db.Payments.Add(payment);
                await db.SaveChangesAsync(ct);
                return 0;
            }, ct);

            return new OrderPendingPaymentCreateResult(true, payment.Id, false);
        }
        catch (DbUpdateException ex) when (IsInflowRequiresInvoiceConstraintViolation(ex))
        {
            return new OrderPendingPaymentCreateResult(false, null, true);
        }
    }

    public async Task<(bool IsValid, int Amount, string? Error)> ResolveAndValidatePaymentAmountAsync(
        long requestedAmount,
        string normalizedPaymentType,
        long? resolvedInvoiceId,
        long? serviceOrderId,
        long? retailOrderId,
        CancellationToken ct = default)
    {
        if (requestedAmount > int.MaxValue)
        {
            return (false, 0, "Số tiền thanh toán vượt quá giới hạn cho phép.");
        }

        if (retailOrderId.HasValue && retailOrderId.Value > 0)
        {
            var retailOrder = await db.RetailOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == retailOrderId.Value, ct);
            if (retailOrder == null)
            {
                return (false, 0, "Không tìm thấy đơn hàng bán lẻ.");
            }

            var paidAmount = await db.Payments
                .AsNoTracking()
                .Where(p => p.RetailOrderId == retailOrder.Id && p.Status == PaymentStatusCompleted)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

            var remainingDue = Math.Max(0m, retailOrder.FinalAmount - paidAmount);
            if (remainingDue <= 0m)
            {
                return (false, 0, "Đơn hàng bán lẻ đã thanh toán đủ.");
            }

            var expectedInt = (int)Math.Ceiling(remainingDue);
            if (requestedAmount > 0 && Math.Abs(requestedAmount - expectedInt) > 100)
            {
                return (false, 0, "Số tiền thanh toán không khớp với giá trị đơn hàng.");
            }

            return (true, expectedInt, null);
        }

        if (resolvedInvoiceId.HasValue && resolvedInvoiceId.Value > 0)
        {
            var invoice = await db.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == resolvedInvoiceId.Value, ct);
            if (invoice == null)
            {
                return (false, 0, "Không tìm thấy hóa đơn.");
            }

            var paidAmount = await db.Payments
                .AsNoTracking()
                .Where(p => p.InvoiceId == invoice.Id && p.Status == PaymentStatusCompleted)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

            var totalAmount = invoice.TotalAmount ?? 0m;
            var remainingDue = Math.Max(0m, totalAmount - paidAmount);
            if (remainingDue <= 0m)
            {
                return (false, 0, "Hóa đơn đã thanh toán đủ.");
            }

            var remainingInt = (int)Math.Ceiling(remainingDue);
            if (requestedAmount <= 0)
            {
                return (true, remainingInt, null);
            }

            if (requestedAmount > remainingInt + 100)
            {
                return (false, 0, "Số tiền thanh toán vượt quá số tiền còn lại của hóa đơn.");
            }

            if (!string.Equals(normalizedPaymentType, "DEPOSIT", StringComparison.OrdinalIgnoreCase) &&
                requestedAmount < 1000)
            {
                return (false, 0, "Số tiền thanh toán tối thiểu là 1.000 VND.");
            }

            return (true, (int)requestedAmount, null);
        }

        if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
        {
            var order = await db.ServiceOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == serviceOrderId.Value, ct);
            if (order == null)
            {
                return (false, 0, "Không tìm thấy lệnh sửa chữa.");
            }

            var paidAmount = await db.Payments
                .AsNoTracking()
                .Where(p => p.ServiceOrderId == order.Id && p.Status == PaymentStatusCompleted)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

            var totalAmount = order.FinalAmount ?? 0m;
            var remainingDue = Math.Max(0m, totalAmount - paidAmount);
            if (remainingDue <= 0m)
            {
                return (false, 0, "Lệnh sửa chữa đã thanh toán đủ.");
            }

            var remainingInt = (int)Math.Ceiling(remainingDue);
            if (requestedAmount <= 0)
            {
                return (true, remainingInt, null);
            }

            if (requestedAmount > remainingInt + 100)
            {
                return (false, 0, "Số tiền thanh toán vượt quá số tiền còn lại của lệnh sửa chữa.");
            }

            return (true, (int)requestedAmount, null);
        }

        return (false, 0, "Thiếu thông tin chứng từ thanh toán hợp lệ.");
    }
}
