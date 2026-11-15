using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Payments;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public sealed record OrderCommandResult(int StatusCode, object? Payload = null)
    {
        public static OrderCommandResult Ok(object? payload = null) => new(200, payload);
        public static OrderCommandResult NoContent() => new(204, null);
        public static OrderCommandResult BadRequest(object? payload) => new(400, payload);
        public static OrderCommandResult Unauthorized() => new(401, null);
        public static OrderCommandResult Forbid() => new(403, null);
        public static OrderCommandResult NotFound(object? payload = null) => new(404, payload);
    }

    public sealed record OrderCommandResult<T>(int StatusCode, T? Data = default, object? ErrorPayload = null)
    {
        public static OrderCommandResult<T> Ok(T data) => new(200, data, null);
        public static OrderCommandResult<T> Created(T data) => new(201, data, null);
        public static OrderCommandResult<T> BadRequest(object? errorPayload) => new(400, default, errorPayload);
        public static OrderCommandResult<T> Unauthorized() => new(401, default, null);
        public static OrderCommandResult<T> Forbid() => new(403, default, null);
        public static OrderCommandResult<T> NotFound(object? errorPayload = null) => new(404, default, errorPayload);
    }

    public sealed record PreparedOrderPaymentContext(
        long OrderCode,
        int ServerAmount,
        string PayOsDescription,
        string ReturnUrl,
        string CancelUrl,
        long? EffectiveServiceOrderId,
        long? EffectiveRetailOrderId,
        long? ResolvedInvoiceId,
        string NormalizedPaymentType,
        string? NormalizedPayerType,
        long? EffectivePayerId);

    public sealed record PayOsCreateLinkOutcome(
        string? PaymentLinkId,
        string? QrCode,
        string? CheckoutUrl,
        PayOsLinkStatus Status,
        long Amount,
        string? Bin,
        string? AccountNumber,
        string? AccountName,
        string? Currency);

    public sealed record PayOsTransactionSnapshot(
        string Reference,
        long Amount,
        string AccountNumber,
        string Description,
        string? TransactionDateTime,
        string? VirtualAccountName,
        string? VirtualAccountNumber,
        string? CounterAccountBankId,
        string? CounterAccountBankName,
        string? CounterAccountName,
        string? CounterAccountNumber);

    public sealed record PayOsLinkSnapshot(
        PayOsLinkStatus Status,
        string StatusRaw,
        long Amount,
        long AmountPaid,
        long AmountRemaining,
        string? CreatedAt,
        string? CanceledAt,
        string? CancellationReason,
        IReadOnlyList<PayOsTransactionSnapshot>? Transactions);

    public sealed record PayOsInvoiceSnapshot(
        string InvoiceId,
        string? InvoiceNumber,
        long? IssuedTimestamp,
        DateTime? IssuedDatetime,
        string? TransactionId,
        string? ReservationCode,
        string? CodeOfTax);

    public interface IOrderCommandFacade
    {
        Task<OrderCommandResult<PreparedOrderPaymentContext>> PrepareCreatePaymentAsync(
            OrderCreateRequest request,
            long? currentUserId,
            bool isStaff);

        Task<OrderCommandResult<Order>> CompleteCreatePaymentAsync(
            OrderCreateRequest request,
            PreparedOrderPaymentContext context,
            PayOsCreateLinkOutcome payOsOutcome);

        Task<OrderCommandResult<Order>> GetAccessibleOrderAsync(
            int orderId,
            long? currentUserId,
            bool isStaff);

        Task<Order> SyncOrderFromPayOsAsync(Order order, PayOsLinkSnapshot snapshot);

        Task SyncCanceledOrderFromPayOsAsync(Order order, PayOsLinkSnapshot snapshot);

        Task<OrderInvoicesInfo> SyncInvoicesAsync(Order order, IReadOnlyList<PayOsInvoiceSnapshot> invoices);

        Task<OrderCommandResult<Order>> GetAccessibleOrderForInvoiceDownloadAsync(
            int orderId,
            string invoiceId,
            long? currentUserId,
            bool isStaff);

        Task<OrderCommandResult<Order>> CreatePaymentAsync(
            OrderCreateRequest request,
            long? currentUserId,
            bool isStaff,
            CancellationToken ct = default);

        Task<OrderCommandResult<Order>> GetOrderWithSyncAsync(
            int orderId,
            long? currentUserId,
            bool isStaff,
            CancellationToken ct = default);

        Task<OrderCommandResult<PayOsLinkSnapshot>> CancelPaymentAsync(
            int orderId,
            string? cancellationReason,
            long? currentUserId,
            bool isStaff,
            CancellationToken ct = default);

        Task<OrderCommandResult<OrderInvoicesInfo>> GetInvoicesWithSyncAsync(
            int orderId,
            long? currentUserId,
            bool isStaff,
            CancellationToken ct = default);

        Task<OrderCommandResult<PayOsInvoiceFileResult>> DownloadInvoiceAsync(
            int orderId,
            string invoiceId,
            long? currentUserId,
            bool isStaff,
            CancellationToken ct = default);
    }
}
