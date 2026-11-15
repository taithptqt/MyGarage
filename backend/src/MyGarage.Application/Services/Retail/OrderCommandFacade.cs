using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Payments;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class OrderCommandFacade : IOrderCommandFacade
    {
        private const string PaymentStatusPending = nameof(PaymentStatusCode.PENDING);
        private const string PaymentStatusCompleted = nameof(PaymentStatusCode.COMPLETED);
        private const string PaymentStatusCanceled = nameof(PaymentStatusCode.CANCELED);
        private static long _orderCodeSeed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private readonly IOrderDataService _dataService;
        private readonly IInvoiceService _invoiceService;
        private readonly IPaymentConfirmationService _paymentConfirmationService;
        private readonly IOrderStore _orderStore;
        private readonly IOrderTransactionStore _orderTransactionStore;
        private readonly IOrderInvoiceStore _orderInvoiceStore;
        private readonly IPayOsGateway _payOsGateway;

        public OrderCommandFacade(
            IOrderDataService dataService,
            IInvoiceService invoiceService,
            IPaymentConfirmationService paymentConfirmationService,
            IOrderStore orderStore,
            IOrderTransactionStore orderTransactionStore,
            IOrderInvoiceStore orderInvoiceStore,
            IPayOsGateway payOsGateway)
        {
            _dataService = dataService;
            _invoiceService = invoiceService;
            _paymentConfirmationService = paymentConfirmationService;
            _orderStore = orderStore;
            _orderTransactionStore = orderTransactionStore;
            _orderInvoiceStore = orderInvoiceStore;
            _payOsGateway = payOsGateway;
        }

        public async Task<OrderCommandResult<PreparedOrderPaymentContext>> PrepareCreatePaymentAsync(
            OrderCreateRequest request,
            long? currentUserId,
            bool isStaff)
        {
            var normalizedPaymentType = NormalizePaymentType(request.PaymentType);

            long? effectiveServiceOrderId = request.ServiceOrderId;
            long? effectiveRetailOrderId = request.RetailOrderId;
            string? normalizedPayerType = isStaff ? NormalizePayerType(request.PayerType) : "CUSTOMER";
            long? effectivePayerId = isStaff ? request.PayerId : currentUserId;

            if (!isStaff)
            {
                if (!currentUserId.HasValue)
                {
                    return OrderCommandResult<PreparedOrderPaymentContext>.Unauthorized();
                }

                if (effectiveRetailOrderId.HasValue && effectiveRetailOrderId.Value > 0)
                {
                    var ownerId = await _dataService.GetRetailOrderCustomerIdAsync(effectiveRetailOrderId.Value);
                    if (ownerId != currentUserId.Value)
                    {
                        return OrderCommandResult<PreparedOrderPaymentContext>.Forbid();
                    }
                }

                if (effectiveServiceOrderId.HasValue && effectiveServiceOrderId.Value > 0)
                {
                    var ownerId = await _dataService.GetServiceOrderCustomerIdAsync(effectiveServiceOrderId.Value);
                    if (ownerId != currentUserId.Value)
                    {
                        return OrderCommandResult<PreparedOrderPaymentContext>.Forbid();
                    }
                }

                if (request.InvoiceId.HasValue && request.InvoiceId.Value > 0)
                {
                    var invScope = await _dataService.GetInvoiceScopeAsync(request.InvoiceId.Value);
                    if (invScope == null ||
                        (invScope.ServiceCustomerId != currentUserId.Value &&
                         invScope.RetailCustomerId != (currentUserId ?? 0)))
                    {
                        return OrderCommandResult<PreparedOrderPaymentContext>.Forbid();
                    }

                    effectiveServiceOrderId ??= invScope.ServiceOrderId;
                    effectiveRetailOrderId ??= invScope.RetailOrderId;
                }
            }

            var resolvedInvoiceId = request.InvoiceId.HasValue && request.InvoiceId.Value > 0
                ? request.InvoiceId.Value
                : (await _invoiceService.EnsureForPaymentContextAsync(
                    effectiveServiceOrderId,
                    effectiveRetailOrderId,
                    normalizedPayerType,
                    effectivePayerId))?.Id ?? await _dataService.ResolveInvoiceIdAsync(
                    request.InvoiceId,
                    request.ServiceOrderId,
                    request.RetailOrderId);

            if (!effectiveRetailOrderId.HasValue && !resolvedInvoiceId.HasValue)
            {
                return OrderCommandResult<PreparedOrderPaymentContext>.BadRequest(new
                {
                    message = "Cannot create inflow payment without invoiceId. Please create/select an invoice first.",
                    serviceOrderId = effectiveServiceOrderId,
                    invoiceId = request.InvoiceId
                });
            }

            var (amountValid, serverAmount, amountError) = await _dataService.ResolveAndValidatePaymentAmountAsync(
                request.TotalAmount,
                normalizedPaymentType,
                resolvedInvoiceId,
                effectiveServiceOrderId,
                effectiveRetailOrderId);
            if (!amountValid)
            {
                return OrderCommandResult<PreparedOrderPaymentContext>.BadRequest(new { message = amountError });
            }

            var orderCode = GenerateOrderCode();
            var returnUrl = request.ReturnUrl ?? "https://your-domain.com/success";
            var cancelUrl = request.CancelUrl ?? "https://your-domain.com/cancel";
            var payOsDescription = BuildPayOsDescription(request.Description, orderCode);

            return OrderCommandResult<PreparedOrderPaymentContext>.Ok(new PreparedOrderPaymentContext(
                orderCode,
                serverAmount,
                payOsDescription,
                returnUrl,
                cancelUrl,
                effectiveServiceOrderId,
                effectiveRetailOrderId,
                resolvedInvoiceId,
                normalizedPaymentType,
                normalizedPayerType,
                effectivePayerId));
        }

        public async Task<OrderCommandResult<Order>> CompleteCreatePaymentAsync(
            OrderCreateRequest request,
            PreparedOrderPaymentContext context,
            PayOsCreateLinkOutcome payOsOutcome)
        {
            var order = new Order
            {
                Id = 0, // Will be set by service
                OrderCode = context.OrderCode,
                RetailOrderId = context.EffectiveRetailOrderId,
                TotalAmount = context.ServerAmount,
                Description = context.PayOsDescription,
                PaymentLinkId = payOsOutcome.PaymentLinkId,
                QrCode = payOsOutcome.QrCode,
                CheckoutUrl = payOsOutcome.CheckoutUrl,
                Status = payOsOutcome.Status,
                Amount = payOsOutcome.Amount,
                AmountPaid = 0,
                AmountRemaining = payOsOutcome.Amount,
                Bin = payOsOutcome.Bin,
                AccountNumber = payOsOutcome.AccountNumber,
                AccountName = payOsOutcome.AccountName,
                Currency = payOsOutcome.Currency,
                ReturnUrl = context.ReturnUrl,
                CancelUrl = context.CancelUrl,
                CreatedAt = VietnamTime.OffsetNow,
                BuyerName = request.BuyerName,
                BuyerCompanyName = request.BuyerCompanyName,
                BuyerEmail = request.BuyerEmail,
                BuyerPhone = request.BuyerPhone,
                BuyerAddress = request.BuyerAddress,
                ExpiredAt = request.ExpiredAt,
                BuyerNotGetInvoice = request.BuyerNotGetInvoice,
                TaxPercentage = request.TaxPercentage,
                Items = [.. request.Items.Select(i => new OrderItem
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Price = i.Price,
                    Unit = i.Unit,
                    TaxPercentage = i.TaxPercentage
                })]
            };

            // DB: INFLOW requires InvoiceId; retail PayOS without invoice must be OUTFLOW (CHK_Payments_InflowRequiresInvoice).
            var paymentDirection = context.ResolvedInvoiceId is > 0 ? "INFLOW" : "OUTFLOW";

            var payment = new Payment
            {
                ServiceOrderId = context.EffectiveServiceOrderId,
                InvoiceId = context.ResolvedInvoiceId,
                RetailOrderId = context.EffectiveRetailOrderId,
                PayerType = context.NormalizedPayerType,
                PayerId = context.EffectivePayerId,
                MethodId = await _dataService.ResolvePayOsMethodIdAsync(),
                Amount = context.ServerAmount,
                TransactionCode = payOsOutcome.PaymentLinkId,
                Type = context.NormalizedPaymentType,
                Direction = paymentDirection,
                Status = PaymentStatusPending,
                CreatedAt = VietnamTime.Now,
                UpdatedAt = VietnamTime.Now
            };

            var createPaymentResult = await _dataService.TryCreatePendingPaymentAsync(payment);
            if (createPaymentResult.Success)
            {
                order.PaymentRecordId = createPaymentResult.PaymentId;
            }
            else if (createPaymentResult.InflowRequiresInvoiceViolation)
            {
                if (request.RetailOrderId.HasValue)
                {
                    // DB currently enforces invoice-only inflow payments.
                    // Keep retail checkout/payment flow working by storing PayOS order without Payments row.
                    order.PaymentRecordId = null;
                }
                else
                {
                    return OrderCommandResult<Order>.BadRequest(new
                    {
                        message = "Payments inflow requires InvoiceId (CHK_Payments_InflowRequiresInvoice).",
                        serviceOrderId = request.ServiceOrderId,
                        invoiceId = context.ResolvedInvoiceId
                    });
                }
            }

            _orderStore.CreateOrder(order);
            return OrderCommandResult<Order>.Created(order);
        }

        public async Task<OrderCommandResult<Order>> GetAccessibleOrderAsync(
            int orderId,
            long? currentUserId,
            bool isStaff)
        {
            var order = _orderStore.GetOrderById(orderId);
            if (order == null || string.IsNullOrEmpty(order.PaymentLinkId))
            {
                return OrderCommandResult<Order>.NotFound();
            }

            if (!await CanAccessOrderAsync(order, currentUserId, isStaff))
            {
                return OrderCommandResult<Order>.Forbid();
            }

            return OrderCommandResult<Order>.Ok(order);
        }

        public async Task<Order> SyncOrderFromPayOsAsync(Order order, PayOsLinkSnapshot snapshot)
        {
            order.Status = snapshot.Status;
            order.Amount = snapshot.Amount;
            order.AmountPaid = snapshot.AmountPaid;
            order.AmountRemaining = snapshot.AmountRemaining;
            order.CreatedAt = DateTimeOffset.TryParse(snapshot.CreatedAt, out var createdAt) ? createdAt : null;
            order.CanceledAt = DateTimeOffset.TryParse(snapshot.CanceledAt, out var canceledAt) ? canceledAt : null;
            order.CancellationReason = snapshot.CancellationReason;

            SyncTransactions(order, snapshot.Transactions);
            _orderStore.UpdateOrder(order.Id, order);

            var payment = await _dataService.ResolvePaymentAsync(order.PaymentRecordId, order.PaymentLinkId);
            if (payment is not null)
            {
                var targetStatus = snapshot.StatusRaw.Contains("CANCEL", StringComparison.OrdinalIgnoreCase)
                    ? PaymentStatusCanceled
                    : (order.AmountRemaining > 0 ? PaymentStatusPending : PaymentStatusCompleted);
                var amountPaid = order.AmountPaid > 0 ? order.AmountPaid : order.Amount;
                await _paymentConfirmationService.ApplyOutcomeAsync(payment, amountPaid, targetStatus);

                if (order.RetailOrderId.HasValue && string.Equals(targetStatus, PaymentStatusCompleted, StringComparison.OrdinalIgnoreCase))
                {
                    await _dataService.EnsureRetailOrderStockDeductedAsync(order.RetailOrderId.Value);
                }
            }
            else if (order.RetailOrderId.HasValue)
            {
                var amtPaid = order.AmountPaid > 0 ? order.AmountPaid : order.Amount;
                await UpdateRetailOrderStatusDirectlyAsync(
                    order.RetailOrderId.Value,
                    order.AmountRemaining <= 0,
                    amtPaid,
                    order.PaymentLinkId ?? "",
                    DateTimeOffset.Now,
                    order.BuyerEmail,
                    order.BuyerName);
            }

            return order;
        }

        public async Task SyncCanceledOrderFromPayOsAsync(Order order, PayOsLinkSnapshot snapshot)
        {
            order.Status = snapshot.Status;
            order.Amount = snapshot.Amount;
            order.AmountPaid = snapshot.AmountPaid;
            order.AmountRemaining = snapshot.AmountRemaining;
            order.CanceledAt = DateTimeOffset.TryParse(snapshot.CanceledAt, out var canceledAt) ? canceledAt : null;
            order.CancellationReason = snapshot.CancellationReason;

            SyncTransactions(order, snapshot.Transactions);
            _orderStore.UpdateOrder(order.Id, order);

            var payment = await _dataService.ResolvePaymentAsync(order.PaymentRecordId, order.PaymentLinkId);
            if (payment is not null)
            {
                await _paymentConfirmationService.ApplyOutcomeAsync(payment, order.AmountPaid, PaymentStatusCanceled);
            }
            else if (order.RetailOrderId.HasValue)
            {
                await UpdateRetailOrderStatusDirectlyAsync(order.RetailOrderId.Value, false);
            }
        }

        public Task<OrderInvoicesInfo> SyncInvoicesAsync(Order order, IReadOnlyList<PayOsInvoiceSnapshot> invoices)
        {
            _orderInvoiceStore.DeleteInvoicesByOrderId(order.Id);

            var orderInvoices = invoices.Select(i => new OrderInvoice
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PaymentLinkId = order.PaymentLinkId!,
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.InvoiceNumber,
                IssuedTimestamp = i.IssuedTimestamp,
                IssuedDatetime = i.IssuedDatetime,
                TransactionId = i.TransactionId,
                ReservationCode = i.ReservationCode,
                CodeOfTax = i.CodeOfTax
            }).ToList();

            _orderInvoiceStore.CreateInvoices(orderInvoices);

            return Task.FromResult(new OrderInvoicesInfo
            {
                Invoices = orderInvoices
            });
        }

        public async Task<OrderCommandResult<Order>> GetAccessibleOrderForInvoiceDownloadAsync(
            int orderId,
            string invoiceId,
            long? currentUserId,
            bool isStaff)
        {
            var accessResult = await GetAccessibleOrderAsync(orderId, currentUserId, isStaff);
            if (accessResult.StatusCode != 200 || accessResult.Data == null)
            {
                return accessResult;
            }

            var invoice = _orderInvoiceStore.GetInvoicesByOrderId(orderId)
                .FirstOrDefault(i => i.InvoiceId == invoiceId);
            if (invoice == null)
            {
                return OrderCommandResult<Order>.NotFound("Invoice not found for this order");
            }

            return accessResult;
        }

        public async Task<OrderCommandResult<Order>> CreatePaymentAsync(
            OrderCreateRequest request,
            long? currentUserId,
            bool isStaff,
            CancellationToken ct = default)
        {
            var prepResult = await PrepareCreatePaymentAsync(request, currentUserId, isStaff);
            if (prepResult.StatusCode != 200 || prepResult.Data == null)
            {
                return new OrderCommandResult<Order>(prepResult.StatusCode, default, prepResult.ErrorPayload);
            }

            var ctx = prepResult.Data;
            var outcome = await _payOsGateway.CreatePaymentLinkAsync(request, ctx, ct);
            return await CompleteCreatePaymentAsync(request, ctx, outcome);
        }

        public async Task<OrderCommandResult<Order>> GetOrderWithSyncAsync(
            int orderId,
            long? currentUserId,
            bool isStaff,
            CancellationToken ct = default)
        {
            var accessResult = await GetAccessibleOrderAsync(orderId, currentUserId, isStaff);
            if (accessResult.StatusCode != 200 || accessResult.Data == null)
            {
                return accessResult;
            }

            var order = accessResult.Data;
            var snapshot = await _payOsGateway.GetPaymentLinkAsync(order.PaymentLinkId!, ct);
            var synced = await SyncOrderFromPayOsAsync(order, snapshot);
            return OrderCommandResult<Order>.Ok(synced);
        }

        public async Task<OrderCommandResult<PayOsLinkSnapshot>> CancelPaymentAsync(
            int orderId,
            string? cancellationReason,
            long? currentUserId,
            bool isStaff,
            CancellationToken ct = default)
        {
            var accessResult = await GetAccessibleOrderAsync(orderId, currentUserId, isStaff);
            if (accessResult.StatusCode != 200 || accessResult.Data == null)
            {
                return new OrderCommandResult<PayOsLinkSnapshot>(accessResult.StatusCode, default, accessResult.ErrorPayload);
            }

            var order = accessResult.Data;
            var snapshot = await _payOsGateway.CancelPaymentLinkAsync(
                order.PaymentLinkId!,
                cancellationReason ?? "Canceled by user",
                ct);
            await SyncCanceledOrderFromPayOsAsync(order, snapshot);
            return OrderCommandResult<PayOsLinkSnapshot>.Ok(snapshot);
        }

        public async Task<OrderCommandResult<OrderInvoicesInfo>> GetInvoicesWithSyncAsync(
            int orderId,
            long? currentUserId,
            bool isStaff,
            CancellationToken ct = default)
        {
            var accessResult = await GetAccessibleOrderAsync(orderId, currentUserId, isStaff);
            if (accessResult.StatusCode != 200 || accessResult.Data == null)
            {
                return new OrderCommandResult<OrderInvoicesInfo>(accessResult.StatusCode, default, accessResult.ErrorPayload);
            }

            var order = accessResult.Data;
            var snapshots = await _payOsGateway.GetInvoicesAsync(order.PaymentLinkId!, ct);
            var orderInvoicesInfo = await SyncInvoicesAsync(order, snapshots);
            return OrderCommandResult<OrderInvoicesInfo>.Ok(orderInvoicesInfo);
        }

        public async Task<OrderCommandResult<PayOsInvoiceFileResult>> DownloadInvoiceAsync(
            int orderId,
            string invoiceId,
            long? currentUserId,
            bool isStaff,
            CancellationToken ct = default)
        {
            var accessResult = await GetAccessibleOrderForInvoiceDownloadAsync(orderId, invoiceId, currentUserId, isStaff);
            if (accessResult.StatusCode != 200 || accessResult.Data == null)
            {
                return new OrderCommandResult<PayOsInvoiceFileResult>(accessResult.StatusCode, default, accessResult.ErrorPayload);
            }

            var order = accessResult.Data;
            var invoiceFile = await _payOsGateway.DownloadInvoiceAsync(invoiceId, order.PaymentLinkId!, ct);
            return OrderCommandResult<PayOsInvoiceFileResult>.Ok(invoiceFile);
        }

        private void SyncTransactions(Order order, IReadOnlyList<PayOsTransactionSnapshot>? transactions)
        {
            if (transactions == null || transactions.Count == 0)
            {
                return;
            }

            _orderTransactionStore.DeleteTransactionsByOrderId(order.Id);

            var entities = transactions.Select(t => new OrderTransaction
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PaymentLinkId = order.PaymentLinkId!,
                Reference = t.Reference,
                Amount = t.Amount,
                AccountNumber = t.AccountNumber,
                Description = t.Description,
                TransactionDateTime = DateTimeOffset.TryParse(t.TransactionDateTime, out var transactionDateTime)
                    ? transactionDateTime
                    : VietnamTime.OffsetNow,
                VirtualAccountName = t.VirtualAccountName,
                VirtualAccountNumber = t.VirtualAccountNumber,
                CounterAccountBankId = t.CounterAccountBankId,
                CounterAccountBankName = t.CounterAccountBankName,
                CounterAccountName = t.CounterAccountName,
                CounterAccountNumber = t.CounterAccountNumber
            }).ToList();

            _orderTransactionStore.CreateTransactions(entities);
            order.LastTransactionUpdate = VietnamTime.OffsetNow;
        }

        private async Task UpdateRetailOrderStatusDirectlyAsync(
            long retailOrderId,
            bool isPaid,
            decimal amountPaid = 0,
            string transactionCode = "",
            DateTimeOffset? transactionTime = null,
            string? fallbackEmail = null,
            string? fallbackFullName = null)
        {
            await _dataService.UpdateRetailOrderStatusDirectlyAsync(retailOrderId, isPaid);

            if (isPaid)
            {
                await _paymentConfirmationService.SendRetailOrderSuccessEmailAsync(
                    retailOrderId,
                    amountPaid,
                    transactionCode,
                    "COMPLETED",
                    transactionTime ?? DateTimeOffset.Now,
                    fallbackEmail,
                    fallbackFullName);
            }
        }

        private async Task<bool> CanAccessOrderAsync(Order order, long? currentUserId, bool isStaff)
        {
            if (isStaff)
            {
                return true;
            }

            if (!currentUserId.HasValue)
            {
                return false;
            }

            if (order.RetailOrderId.HasValue)
            {
                var ownerId = await _dataService.GetRetailOrderCustomerIdAsync(order.RetailOrderId.Value);
                if (ownerId == currentUserId.Value)
                {
                    return true;
                }
            }

            var payment = await _dataService.ResolvePaymentAsync(order.PaymentRecordId, order.PaymentLinkId);
            if (payment == null)
            {
                return false;
            }

            if (string.Equals(payment.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) &&
                payment.PayerId == currentUserId.Value)
            {
                return true;
            }

            if (payment.ServiceOrderId.HasValue)
            {
                var ownerId = await _dataService.GetServiceOrderCustomerIdAsync(payment.ServiceOrderId.Value);
                if (ownerId == currentUserId.Value)
                {
                    return true;
                }
            }

            if (payment.RetailOrderId.HasValue)
            {
                var ownerId = await _dataService.GetRetailOrderCustomerIdAsync(payment.RetailOrderId.Value);
                if (ownerId == currentUserId.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private static long GenerateOrderCode()
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            while (true)
            {
                var current = Interlocked.Read(ref _orderCodeSeed);
                var next = nowMs > current ? nowMs : current + 1;
                var original = Interlocked.CompareExchange(ref _orderCodeSeed, next, current);

                if (original == current)
                {
                    return next;
                }
            }
        }

        private static string NormalizePaymentType(string? paymentType)
        {
            var normalized = paymentType?.Trim().ToUpperInvariant();
            return normalized switch
            {
                "PAYMENT" or "DEPOSIT" or "REFUND" or "CUSTOMER_CREDIT" or "REVERSAL" => normalized,
                _ => "PAYMENT"
            };
        }

        private static string? NormalizePayerType(string? payerType)
        {
            var normalized = payerType?.Trim().ToUpperInvariant();
            return normalized switch
            {
                "CUSTOMER" or "INSURANCE" => normalized,
                _ => null
            };
        }

        private static string BuildPayOsDescription(string? description, long orderCode)
        {
            var raw = string.IsNullOrWhiteSpace(description) ? $"order {orderCode}" : description.Trim();
            return raw.Length <= 25 ? raw : raw[..25];
        }
    }
}
