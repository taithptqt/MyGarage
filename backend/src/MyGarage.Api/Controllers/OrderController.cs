using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Enums;
using MyGarage.Services;
using MyGarage.Support;

using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.V2.PaymentRequests.Invoices;

using PayOS;

namespace MyGarage.Controllers;

[ApiController]
[Route("api/orders")]
[Route("api/v1/orders")]
public class OrderController(
    [FromKeyedServices("OrderClient")] PayOSClient client,
    IGarageContext db,
    IInvoiceService invoiceService,
    IPaymentConfirmationService paymentConfirmationService,
    IOrderStore orderStore,
    IOrderTransactionStore orderTransactionStore,
    IOrderInvoiceStore orderInvoiceStore) : ControllerBase
{
    private const string RetailOrderStatusConfirmed = nameof(RetailOrderStatus.CONFIRMED);
    private const string RetailOrderStatusDraft = nameof(RetailOrderStatus.DRAFT);
    private const string PaymentStatusPending = nameof(PaymentStatusCode.PENDING);
    private const string PaymentStatusCompleted = nameof(PaymentStatusCode.COMPLETED);
    private const string PaymentStatusCanceled = nameof(PaymentStatusCode.CANCELED);
    private static long _orderCodeSeed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private readonly PayOSClient _client = client;
    private readonly IGarageContext _db = db;
    private readonly IInvoiceService _invoiceService = invoiceService;
    private readonly IPaymentConfirmationService _paymentConfirmationService = paymentConfirmationService;
    private readonly IOrderStore _orderStore = orderStore;
    private readonly IOrderTransactionStore _orderTransactionStore = orderTransactionStore;
    private readonly IOrderInvoiceStore _orderInvoiceStore = orderInvoiceStore;

    private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                var result = await action();
                if (tx != null)
                {
                    await tx.CommitAsync();
                }

                return result;
            }
            catch
            {
                if (tx != null)
                {
                    await tx.RollbackAsync();
                }

                throw;
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_db.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _db.Database.BeginTransactionAsync();
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

    private async Task UpdateRetailOrderStatusDirectlyAsync(
        long retailOrderId,
        bool isPaid,
        decimal amountPaid = 0,
        string transactionCode = "",
        DateTimeOffset? transactionTime = null,
        string? fallbackEmail = null,
        string? fallbackFullName = null)
    {
        var retailOrder = await _db.RetailOrders.FirstOrDefaultAsync(x => x.Id == retailOrderId);
        if (retailOrder is null)
        {
            return;
        }

        await ExecuteInTransactionAsync(async () =>
        {
            retailOrder.Status = isPaid ? RetailOrderStatusConfirmed : RetailOrderStatusDraft;
            retailOrder.UpdatedAt = VietnamTime.Now;
            await _db.SaveChangesAsync();
            return 0;
        });

        if (isPaid)
        {
            await RetailOrderStockCommitter.EnsureStockDeductedAsync(_db, retailOrderId);

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

    private async Task<long> ResolvePayOsMethodIdAsync()
    {
        var method = await _db.PaymentMethods
            .FirstOrDefaultAsync(m => m.MethodCode == "PAYOS" && m.IsDeleted != true);

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
            _db.PaymentMethods.Add(method);
            await _db.SaveChangesAsync();
            return method.Id;
        });
    }

    private async Task<long?> ResolveInvoiceIdAsync(OrderCreateRequest request)
    {
        if (request.InvoiceId.HasValue && request.InvoiceId.Value > 0)
        {
            return request.InvoiceId.Value;
        }

        if (request.ServiceOrderId.HasValue && request.ServiceOrderId.Value > 0)
        {
            return await _db.Invoices
                .AsNoTracking()
                .Where(i => i.ServiceOrderId == request.ServiceOrderId.Value)
                .OrderByDescending(i => i.IssuedAt ?? i.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(i => i.Id)
                .Select(i => (long?)i.Id)
                .FirstOrDefaultAsync();
        }

        if (request.RetailOrderId.HasValue && request.RetailOrderId.Value > 0)
        {
            return await _db.Invoices
                .AsNoTracking()
                .Where(i => i.RetailOrderId == request.RetailOrderId.Value)
                .OrderByDescending(i => i.IssuedAt ?? i.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(i => i.Id)
                .Select(i => (long?)i.Id)
                .FirstOrDefaultAsync();
        }

        return null;
    }

    private async Task<Payment?> ResolvePaymentAsync(Order order)
    {
        if (order.PaymentRecordId.HasValue)
        {
            var byId = await _db.Payments.FirstOrDefaultAsync(p => p.Id == order.PaymentRecordId.Value);
            if (byId is not null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(order.PaymentLinkId))
        {
            return await _db.Payments
                .Where(p => p.TransactionCode == order.PaymentLinkId)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
        }

        return null;
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> Get(int id)
    {
        var order = _orderStore.GetOrderById(id);
        if (order == null || string.IsNullOrEmpty(order.PaymentLinkId))
        {
            return NotFound();
        }

        if (!await CanAccessOrderAsync(order))
        {
            return Forbid();
        }

        try
        {
            var paymentLink = await _client.PaymentRequests.GetAsync(order.PaymentLinkId);

            order.Status = paymentLink.Status;
            order.Amount = paymentLink.Amount;
            order.AmountPaid = paymentLink.AmountPaid;
            order.AmountRemaining = paymentLink.AmountRemaining;
            order.CreatedAt = DateTimeOffset.TryParse(paymentLink.CreatedAt, out var createdAt) ? createdAt : null;
            order.CanceledAt = DateTimeOffset.TryParse(paymentLink.CanceledAt, out var canceledAt) ? canceledAt : null;
            order.CancellationReason = paymentLink.CancellationReason;

            if (paymentLink.Transactions != null && paymentLink.Transactions.Count > 0)
            {
                _orderTransactionStore.DeleteTransactionsByOrderId(order.Id);

                var transactions = paymentLink.Transactions.Select(t => new OrderTransaction
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    PaymentLinkId = order.PaymentLinkId,
                    Reference = t.Reference,
                    Amount = t.Amount,
                    AccountNumber = t.AccountNumber,
                    Description = t.Description,
                    TransactionDateTime = DateTimeOffset.TryParse(t.TransactionDateTime, out var transactionDateTime) ? transactionDateTime : VietnamTime.OffsetNow,
                    VirtualAccountName = t.VirtualAccountName,
                    VirtualAccountNumber = t.VirtualAccountNumber,
                    CounterAccountBankId = t.CounterAccountBankId,
                    CounterAccountBankName = t.CounterAccountBankName,
                    CounterAccountName = t.CounterAccountName,
                    CounterAccountNumber = t.CounterAccountNumber
                }).ToList();

                _orderTransactionStore.CreateTransactions(transactions);
                order.LastTransactionUpdate = VietnamTime.OffsetNow;
            }

            _orderStore.UpdateOrder(order.Id, order);

            var payment = await ResolvePaymentAsync(order);
            if (payment is not null)
            {
                var payOsStatus = paymentLink.Status.ToString();
                var targetStatus = payOsStatus.Contains("CANCEL", StringComparison.OrdinalIgnoreCase)
                    ? PaymentStatusCanceled
                    : (order.AmountRemaining > 0 ? PaymentStatusPending : PaymentStatusCompleted);
                var amountPaid = order.AmountPaid > 0 ? order.AmountPaid : order.Amount;
                await _paymentConfirmationService.ApplyOutcomeAsync(payment, amountPaid, targetStatus);

                if (order.RetailOrderId.HasValue && string.Equals(targetStatus, PaymentStatusCompleted, StringComparison.OrdinalIgnoreCase))
                {
                    await RetailOrderStockCommitter.EnsureStockDeductedAsync(_db, order.RetailOrderId.Value);
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

            return Ok(order);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to retrieve order {id}", error = ex.Message });
        }

    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Order>> CreatePayment(OrderCreateRequest request)
    {
        if (request == null)
        {
            return BadRequest("Order data is required");
        }
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var normalizedPaymentType = NormalizePaymentType(request.PaymentType);
        var currentUserId = this.GetCurrentUserId();
        var isStaff = this.IsStaffUser();

        long? effectiveServiceOrderId = request.ServiceOrderId;
        long? effectiveRetailOrderId = request.RetailOrderId;
        string? normalizedPayerType = isStaff ? NormalizePayerType(request.PayerType) : "CUSTOMER";
        long? effectivePayerId = isStaff ? request.PayerId : currentUserId;

        if (!isStaff)
        {
            if (!currentUserId.HasValue)
            {
                return Unauthorized();
            }

            if (effectiveRetailOrderId.HasValue && effectiveRetailOrderId.Value > 0)
            {
                var ownerId = await _db.RetailOrders
                    .AsNoTracking()
                    .Where(r => r.Id == effectiveRetailOrderId.Value)
                    .Select(r => (long?)r.CustomerId)
                    .FirstOrDefaultAsync();
                if (ownerId != currentUserId.Value)
                {
                    return Forbid();
                }
            }

            if (effectiveServiceOrderId.HasValue && effectiveServiceOrderId.Value > 0)
            {
                var ownerId = await _db.ServiceOrders
                    .AsNoTracking()
                    .Where(s => s.Id == effectiveServiceOrderId.Value)
                    .Select(s => (long?)s.CustomerId)
                    .FirstOrDefaultAsync();
                if (ownerId != currentUserId.Value)
                {
                    return Forbid();
                }
            }

            if (request.InvoiceId.HasValue && request.InvoiceId.Value > 0)
            {
                var invScope = await _db.Invoices
                    .AsNoTracking()
                    .Where(i => i.Id == request.InvoiceId.Value)
                    .Select(i => new
                    {
                        ServiceCustomerId = i.ServiceOrder != null ? (long?)i.ServiceOrder.CustomerId : null,
                        RetailCustomerId = i.RetailOrder != null ? (long?)i.RetailOrder.CustomerId : null,
                        i.ServiceOrderId,
                        i.RetailOrderId
                    })
                    .FirstOrDefaultAsync();
                if (invScope == null ||
                    (invScope.ServiceCustomerId != currentUserId.Value &&
                     invScope.RetailCustomerId != currentCustomerIdValue(currentUserId)))
                {
                    return Forbid();
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
                effectivePayerId))?.Id ?? await ResolveInvoiceIdAsync(request);

        if (!effectiveRetailOrderId.HasValue && !resolvedInvoiceId.HasValue)
        {
            return BadRequest(new
            {
                message = "Cannot create inflow payment without invoiceId. Please create/select an invoice first.",
                serviceOrderId = effectiveServiceOrderId,
                invoiceId = request.InvoiceId
            });
        }

        var (amountValid, serverAmount, amountError) = await ResolveAndValidatePaymentAmountAsync(
            request.TotalAmount,
            normalizedPaymentType,
            resolvedInvoiceId,
            effectiveServiceOrderId,
            effectiveRetailOrderId);
        if (!amountValid)
        {
            return BadRequest(new { message = amountError });
        }

        var orderCode = GenerateOrderCode();
        var returnUrl = request.ReturnUrl ?? "https://your-domain.com/success";
        var cancelUrl = request.CancelUrl ?? "https://your-domain.com/cancel";
        var payOsDescription = BuildPayOsDescription(request.Description, orderCode);
        var paymentRequest = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = serverAmount,
            Description = payOsDescription,
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl,
            BuyerName = request.BuyerName,
            BuyerCompanyName = request.BuyerCompanyName,
            BuyerEmail = request.BuyerEmail,
            BuyerPhone = request.BuyerPhone,
            BuyerAddress = request.BuyerAddress,
            ExpiredAt = request.ExpiredAt?.ToUnixTimeSeconds(),
            Items = [.. request.Items.Select(i => new PaymentLinkItem
            {
                Name = i.Name ?? "",
                Quantity = i.Quantity,
                Price = i.Price,
                Unit = i.Unit,
                TaxPercentage = i.TaxPercentage
            })]
        };

        if (request.BuyerNotGetInvoice.HasValue || request.TaxPercentage.HasValue)
        {
            paymentRequest.Invoice = new InvoiceRequest
            {
                BuyerNotGetInvoice = request.BuyerNotGetInvoice,
                TaxPercentage = request.TaxPercentage
            };
        }

        try
        {
            var paymentResponse = await _client.PaymentRequests.CreateAsync(paymentRequest);

            var order = new Order
            {
                Id = 0, // Will be set by service
                OrderCode = orderCode,
                RetailOrderId = effectiveRetailOrderId,
                TotalAmount = serverAmount,
                Description = payOsDescription,
                PaymentLinkId = paymentResponse.PaymentLinkId,
                QrCode = paymentResponse.QrCode,
                CheckoutUrl = paymentResponse.CheckoutUrl,
                Status = paymentResponse.Status,
                Amount = paymentResponse.Amount,
                AmountPaid = 0,
                AmountRemaining = paymentResponse.Amount,
                Bin = paymentResponse.Bin,
                AccountNumber = paymentResponse.AccountNumber,
                AccountName = paymentResponse.AccountName,
                Currency = paymentResponse.Currency,
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl,
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
            var paymentDirection = resolvedInvoiceId is > 0 ? "INFLOW" : "OUTFLOW";

            var payment = new Payment
            {
                ServiceOrderId = effectiveServiceOrderId,
                InvoiceId = resolvedInvoiceId,
                RetailOrderId = effectiveRetailOrderId,
                PayerType = normalizedPayerType,
                PayerId = effectivePayerId,
                MethodId = await ResolvePayOsMethodIdAsync(),
                Amount = serverAmount,
                TransactionCode = paymentResponse.PaymentLinkId,
                Type = normalizedPaymentType,
                Direction = paymentDirection,
                Status = PaymentStatusPending,
                CreatedAt = VietnamTime.Now,
                UpdatedAt = VietnamTime.Now
            };

            try
            {
                await ExecuteInTransactionAsync(async () =>
                {
                    _db.Payments.Add(payment);
                    await _db.SaveChangesAsync();
                    order.PaymentRecordId = payment.Id;
                    return 0;
                });
            }
            catch (DbUpdateException ex) when (IsInflowRequiresInvoiceConstraintViolation(ex))
            {
                if (request.RetailOrderId.HasValue)
                {
                    // DB currently enforces invoice-only inflow payments.
                    // Keep retail checkout/payment flow working by storing PayOS order without Payments row.
                    order.PaymentRecordId = null;
                }
                else
                {
                    return BadRequest(new
                    {
                        message = "Payments inflow requires InvoiceId (CHK_Payments_InflowRequiresInvoice).",
                        serviceOrderId = request.ServiceOrderId,
                        invoiceId = resolvedInvoiceId
                    });
                }
            }

            _orderStore.CreateOrder(order);
            return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to create order", error = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("{orderId}/cancel")]
    [HttpPatch("{orderId}/status")]
    public async Task<ActionResult<PaymentLink>> CancelPayment(int orderId, string cancellationReason)
    {
        var order = _orderStore.GetOrderById(orderId);
        if (order == null || string.IsNullOrEmpty(order.PaymentLinkId))
        {
            return NotFound();
        }

        if (!await CanAccessOrderAsync(order))
        {
            return Forbid();
        }

        try
        {

                var paymentLink = await _client.PaymentRequests.CancelAsync(order.PaymentLinkId, cancellationReason ?? "Canceled by user");

            order.Status = paymentLink.Status;
            order.Amount = paymentLink.Amount;
            order.AmountPaid = paymentLink.AmountPaid;
            order.AmountRemaining = paymentLink.AmountRemaining;
            order.CanceledAt = DateTimeOffset.TryParse(paymentLink.CanceledAt, out var canceledAt) ? canceledAt : null;
            order.CancellationReason = paymentLink.CancellationReason;

            if (paymentLink.Transactions != null && paymentLink.Transactions.Count > 0)
            {
                _orderTransactionStore.DeleteTransactionsByOrderId(order.Id);

                var transactions = paymentLink.Transactions.Select(t => new OrderTransaction
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    PaymentLinkId = order.PaymentLinkId,
                    Reference = t.Reference,
                    Amount = t.Amount,
                    AccountNumber = t.AccountNumber,
                    Description = t.Description,
                    TransactionDateTime = DateTimeOffset.TryParse(t.TransactionDateTime, out var transactionDateTime) ? transactionDateTime : VietnamTime.OffsetNow,
                    VirtualAccountName = t.VirtualAccountName,
                    VirtualAccountNumber = t.VirtualAccountNumber,
                    CounterAccountBankId = t.CounterAccountBankId,
                    CounterAccountBankName = t.CounterAccountBankName,
                    CounterAccountName = t.CounterAccountName,
                    CounterAccountNumber = t.CounterAccountNumber
                }).ToList();

                _orderTransactionStore.CreateTransactions(transactions);
                order.LastTransactionUpdate = VietnamTime.OffsetNow;
            }

            _orderStore.UpdateOrder(order.Id, order);
            var payment = await ResolvePaymentAsync(order);
            if (payment is not null)
            {
                await _paymentConfirmationService.ApplyOutcomeAsync(payment, order.AmountPaid, PaymentStatusCanceled);
            }
            else if (order.RetailOrderId.HasValue)
            {
                await UpdateRetailOrderStatusDirectlyAsync(order.RetailOrderId.Value, false);
            }

            return Ok(paymentLink);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to cancel order {orderId}", error = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("{orderId}/invoices")]
    public async Task<ActionResult<OrderInvoicesInfo>> GetInvoices(int orderId)
    {
        var order = _orderStore.GetOrderById(orderId);
        if (order == null || string.IsNullOrEmpty(order.PaymentLinkId))
        {
            return NotFound();
        }

        if (!await CanAccessOrderAsync(order))
        {
            return Forbid();
        }
        try
        {
            var invoices = await _client.PaymentRequests.Invoices.GetAsync(order.PaymentLinkId);

            _orderInvoiceStore.DeleteInvoicesByOrderId(order.Id);

            var orderInvoices = invoices.Invoices.Select(i => new OrderInvoice
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PaymentLinkId = order.PaymentLinkId,
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.InvoiceNumber,
                IssuedTimestamp = i.IssuedTimestamp,
                IssuedDatetime = i.IssuedDatetime,
                TransactionId = i.TransactionId,
                ReservationCode = i.ReservationCode,
                CodeOfTax = i.CodeOfTax
            }).ToList();

            _orderInvoiceStore.CreateInvoices(orderInvoices);

            var orderInvoicesInfo = new OrderInvoicesInfo
            {
                Invoices = orderInvoices
            };

            return Ok(orderInvoicesInfo);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to retrieve invoice for order {orderId}", error = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("{orderId}/invoices/{invoiceId}/download")]
    [HttpGet("{orderId}/invoices/{invoiceId}")]
    public async Task<ActionResult> DownloadInvoice(int orderId, string invoiceId)
    {
        var order = _orderStore.GetOrderById(orderId);
        if (order == null || string.IsNullOrEmpty(order.PaymentLinkId))
        {
            return NotFound();
        }

        if (!await CanAccessOrderAsync(order))
        {
            return Forbid();
        }

        var invoice = _orderInvoiceStore.GetInvoicesByOrderId(orderId)
            .FirstOrDefault(i => i.InvoiceId == invoiceId);
        if (invoice == null)
        {
            return NotFound("Invoice not found for this order");
        }

        try
        {
            var invoiceFile = await _client.PaymentRequests.Invoices.DownloadAsync(invoiceId, order.PaymentLinkId);

            var fileName = invoiceFile.FileName ?? $"invoice_{invoiceId}.pdf";
            var contentType = invoiceFile.ContentType ?? "application/pdf";

            return File(invoiceFile.Content, contentType, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to download invoice {invoiceId}", error = ex.Message });
        }
    }

    private static long currentCustomerIdValue(long? id) => id ?? 0;

    private async Task<bool> CanAccessOrderAsync(Order order)
    {
        if (this.IsStaffUser())
        {
            return true;
        }

        var currentCustomerId = this.GetCurrentUserId();
        if (!currentCustomerId.HasValue)
        {
            return false;
        }

        if (order.RetailOrderId.HasValue)
        {
            var ownerId = await _db.RetailOrders
                .AsNoTracking()
                .Where(r => r.Id == order.RetailOrderId.Value)
                .Select(r => (long?)r.CustomerId)
                .FirstOrDefaultAsync();
            if (ownerId == currentCustomerId.Value)
            {
                return true;
            }
        }

        var payment = await ResolvePaymentAsync(order);
        if (payment == null)
        {
            return false;
        }

        if (string.Equals(payment.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) &&
            payment.PayerId == currentCustomerId.Value)
        {
            return true;
        }

        if (payment.ServiceOrderId.HasValue)
        {
            var ownerId = await _db.ServiceOrders
                .AsNoTracking()
                .Where(s => s.Id == payment.ServiceOrderId.Value)
                .Select(s => (long?)s.CustomerId)
                .FirstOrDefaultAsync();
            if (ownerId == currentCustomerId.Value)
            {
                return true;
            }
        }

        if (payment.RetailOrderId.HasValue)
        {
            var ownerId = await _db.RetailOrders
                .AsNoTracking()
                .Where(r => r.Id == payment.RetailOrderId.Value)
                .Select(r => (long?)r.CustomerId)
                .FirstOrDefaultAsync();
            if (ownerId == currentCustomerId.Value)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<(bool IsValid, int Amount, string? Error)> ResolveAndValidatePaymentAmountAsync(
        long requestedAmount,
        string normalizedPaymentType,
        long? resolvedInvoiceId,
        long? serviceOrderId,
        long? retailOrderId)
    {
        if (requestedAmount > int.MaxValue)
        {
            return (false, 0, "Số tiền thanh toán vượt quá giới hạn cho phép.");
        }
        if (retailOrderId.HasValue && retailOrderId.Value > 0)
        {
            var retailOrder = await _db.RetailOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == retailOrderId.Value);
            if (retailOrder == null)
            {
                return (false, 0, "Không tìm thấy đơn hàng bán lẻ.");
            }

            var paidAmount = await _db.Payments
                .AsNoTracking()
                .Where(p => p.RetailOrderId == retailOrder.Id && p.Status == PaymentStatusCompleted)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

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
            var invoice = await _db.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == resolvedInvoiceId.Value);
            if (invoice == null)
            {
                return (false, 0, "Không tìm thấy hóa đơn.");
            }

            var paidAmount = await _db.Payments
                .AsNoTracking()
                .Where(p => p.InvoiceId == invoice.Id && p.Status == PaymentStatusCompleted)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

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
            var order = await _db.ServiceOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == serviceOrderId.Value);
            if (order == null)
            {
                return (false, 0, "Không tìm thấy lệnh sửa chữa.");
            }

            var paidAmount = await _db.Payments
                .AsNoTracking()
                .Where(p => p.ServiceOrderId == order.Id && p.Status == PaymentStatusCompleted)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

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
