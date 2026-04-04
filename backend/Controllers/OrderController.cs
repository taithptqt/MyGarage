using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;

using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.V2.PaymentRequests.Invoices;

using PayOS;

namespace MyGarage.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController(
    [FromKeyedServices("OrderClient")] PayOSClient client,
    IGarageContext db,
    IPaymentConfirmationService paymentConfirmationService,
    IOrderStore orderStore,
    IOrderTransactionStore orderTransactionStore,
    IOrderInvoiceStore orderInvoiceStore) : ControllerBase
{
    private readonly PayOSClient _client = client;
    private readonly IGarageContext _db = db;
    private readonly IPaymentConfirmationService _paymentConfirmationService = paymentConfirmationService;
    private readonly IOrderStore _orderStore = orderStore;
    private readonly IOrderTransactionStore _orderTransactionStore = orderTransactionStore;
    private readonly IOrderInvoiceStore _orderInvoiceStore = orderInvoiceStore;

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

    private async Task UpdateRetailOrderStatusDirectlyAsync(long retailOrderId, bool isPaid)
    {
        var retailOrder = await _db.RetailOrders.FirstOrDefaultAsync(x => x.Id == retailOrderId);
        if (retailOrder is null)
        {
            return;
        }

        retailOrder.Status = isPaid ? "CONFIRMED" : "DRAFT";
        retailOrder.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
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

        var now = DateTime.UtcNow;
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

        _db.PaymentMethods.Add(method);
        await _db.SaveChangesAsync();
        return method.Id;
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
                    TransactionDateTime = DateTimeOffset.TryParse(t.TransactionDateTime, out var transactionDateTime) ? transactionDateTime : DateTimeOffset.Now,
                    VirtualAccountName = t.VirtualAccountName,
                    VirtualAccountNumber = t.VirtualAccountNumber,
                    CounterAccountBankId = t.CounterAccountBankId,
                    CounterAccountBankName = t.CounterAccountBankName,
                    CounterAccountName = t.CounterAccountName,
                    CounterAccountNumber = t.CounterAccountNumber
                }).ToList();

                _orderTransactionStore.CreateTransactions(transactions);
                order.LastTransactionUpdate = DateTimeOffset.Now;
            }

            _orderStore.UpdateOrder(order.Id, order);

            var payment = await ResolvePaymentAsync(order);
            if (payment is not null)
            {
                var payOsStatus = paymentLink.Status.ToString();
                var targetStatus = payOsStatus.Contains("CANCEL", StringComparison.OrdinalIgnoreCase)
                    ? "CANCELED"
                    : (order.AmountRemaining > 0 ? "PENDING" : "COMPLETED");
                var amountPaid = order.AmountPaid > 0 ? order.AmountPaid : order.Amount;
                await _paymentConfirmationService.ApplyOutcomeAsync(payment, amountPaid, targetStatus);
            }
            else if (order.RetailOrderId.HasValue)
            {
                await UpdateRetailOrderStatusDirectlyAsync(order.RetailOrderId.Value, order.AmountRemaining <= 0);
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
        var resolvedInvoiceId = await ResolveInvoiceIdAsync(request);

        if (!request.RetailOrderId.HasValue && !resolvedInvoiceId.HasValue)
        {
            return BadRequest(new
            {
                message = "Cannot create inflow payment without invoiceId. Please create/select an invoice first.",
                serviceOrderId = request.ServiceOrderId,
                invoiceId = request.InvoiceId
            });
        }

        var orderCode = DateTimeOffset.Now.ToUnixTimeSeconds();
        var returnUrl = request.ReturnUrl ?? "https://your-domain.com/success";
        var cancelUrl = request.CancelUrl ?? "https://your-domain.com/cancel";
        var payOsDescription = BuildPayOsDescription(request.Description, orderCode);
        var paymentRequest = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = request.TotalAmount,
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
                RetailOrderId = request.RetailOrderId,
                TotalAmount = request.TotalAmount,
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
                CreatedAt = DateTimeOffset.Now,
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

            var payment = new Payment
            {
                ServiceOrderId = request.ServiceOrderId,
                InvoiceId = resolvedInvoiceId,
                RetailOrderId = request.RetailOrderId,
                PayerType = NormalizePayerType(request.PayerType),
                PayerId = request.PayerId,
                MethodId = await ResolvePayOsMethodIdAsync(),
                Amount = request.TotalAmount,
                TransactionCode = paymentResponse.PaymentLinkId,
                Type = normalizedPaymentType,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();
                order.PaymentRecordId = payment.Id;
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

        try
        {

            var paymentLink = await _client.PaymentRequests.CancelAsync(order.PaymentLinkId, cancellationReason ?? "Cancelled by user");

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
                    TransactionDateTime = DateTimeOffset.TryParse(t.TransactionDateTime, out var transactionDateTime) ? transactionDateTime : DateTimeOffset.Now,
                    VirtualAccountName = t.VirtualAccountName,
                    VirtualAccountNumber = t.VirtualAccountNumber,
                    CounterAccountBankId = t.CounterAccountBankId,
                    CounterAccountBankName = t.CounterAccountBankName,
                    CounterAccountName = t.CounterAccountName,
                    CounterAccountNumber = t.CounterAccountNumber
                }).ToList();

                _orderTransactionStore.CreateTransactions(transactions);
                order.LastTransactionUpdate = DateTimeOffset.Now;
            }

            _orderStore.UpdateOrder(order.Id, order);
            var payment = await ResolvePaymentAsync(order);
            if (payment is not null)
            {
                await _paymentConfirmationService.ApplyOutcomeAsync(payment, order.AmountPaid, "CANCELED");
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
}
