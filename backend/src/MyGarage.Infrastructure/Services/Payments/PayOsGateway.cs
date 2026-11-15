using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Payments;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Interfaces;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.V2.PaymentRequests.Invoices;
using PayOS.Models.Webhooks;

namespace MyGarage.Infrastructure.Services.Payments;

public sealed class PayOsGateway(
    [FromKeyedServices("OrderClient")] PayOSClient client) : IPayOsGateway
{
    private static readonly JsonSerializerOptions WebhookJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PayOSClient _client = client;

    public async Task<PayOsCreateLinkOutcome> CreatePaymentLinkAsync(
        OrderCreateRequest request,
        PreparedOrderPaymentContext context,
        CancellationToken ct = default)
    {
        var paymentRequest = new CreatePaymentLinkRequest
        {
            OrderCode = context.OrderCode,
            Amount = context.ServerAmount,
            Description = context.PayOsDescription,
            ReturnUrl = context.ReturnUrl,
            CancelUrl = context.CancelUrl,
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
                TaxPercentage = ToPayOsTaxPercentage(i.TaxPercentage)
            })]
        };

        if (request.BuyerNotGetInvoice.HasValue || request.TaxPercentage.HasValue)
        {
            paymentRequest.Invoice = new InvoiceRequest
            {
                BuyerNotGetInvoice = request.BuyerNotGetInvoice,
                TaxPercentage = ToPayOsTaxPercentage(request.TaxPercentage)
            };
        }

        var paymentResponse = await _client.PaymentRequests.CreateAsync(paymentRequest);
        return new PayOsCreateLinkOutcome(
            paymentResponse.PaymentLinkId,
            paymentResponse.QrCode,
            paymentResponse.CheckoutUrl,
            ToOrderStatus(paymentResponse.Status),
            paymentResponse.Amount,
            paymentResponse.Bin,
            paymentResponse.AccountNumber,
            paymentResponse.AccountName,
            paymentResponse.Currency);
    }

    public async Task<PayOsLinkSnapshot> GetPaymentLinkAsync(
        string paymentLinkId,
        CancellationToken ct = default)
    {
        var paymentLink = await _client.PaymentRequests.GetAsync(paymentLinkId);
        return MapLinkSnapshot(paymentLink);
    }

    public async Task<PayOsLinkSnapshot> CancelPaymentLinkAsync(
        string paymentLinkId,
        string cancellationReason,
        CancellationToken ct = default)
    {
        var paymentLink = await _client.PaymentRequests.CancelAsync(paymentLinkId, cancellationReason);
        return MapLinkSnapshot(paymentLink);
    }

    public async Task<IReadOnlyList<PayOsInvoiceSnapshot>> GetInvoicesAsync(
        string paymentLinkId,
        CancellationToken ct = default)
    {
        var invoices = await _client.PaymentRequests.Invoices.GetAsync(paymentLinkId);
        return invoices.Invoices.Select(i => new PayOsInvoiceSnapshot(
            i.InvoiceId,
            i.InvoiceNumber,
            i.IssuedTimestamp,
            i.IssuedDatetime,
            i.TransactionId,
            i.ReservationCode,
            i.CodeOfTax)).ToList();
    }

    public async Task<PayOsInvoiceFileResult> DownloadInvoiceAsync(
        string invoiceId,
        string paymentLinkId,
        CancellationToken ct = default)
    {
        using var invoiceFile = await _client.PaymentRequests.Invoices.DownloadAsync(invoiceId, paymentLinkId);
        using var memoryStream = new MemoryStream();
        await invoiceFile.Content.CopyToAsync(memoryStream, ct);

        return new PayOsInvoiceFileResult(
            memoryStream.ToArray(),
            invoiceFile.ContentType ?? "application/pdf",
            invoiceFile.FileName ?? $"invoice_{invoiceId}.pdf");
    }

    public async Task ConfirmWebhookAsync(
        string webhookUrl,
        CancellationToken ct = default)
    {
        await _client.Webhooks.ConfirmAsync(webhookUrl);
    }

    public async Task<VerifiedPaymentWebhookInput> VerifyWebhookAsync(
        JsonElement webhookPayload,
        CancellationToken ct = default)
    {
        var webhook = webhookPayload.Deserialize<Webhook>(WebhookJsonOptions)
            ?? throw new InvalidOperationException("Webhook data is required");

        var webhookData = await _client.Webhooks.VerifyAsync(webhook);
        return new VerifiedPaymentWebhookInput(
            webhookData.OrderCode,
            webhookData.Amount,
            webhookData.Reference,
            webhookData.AccountNumber,
            webhookData.Description,
            webhookData.TransactionDateTime,
            webhookData.PaymentLinkId,
            webhookData.VirtualAccountName,
            webhookData.VirtualAccountNumber,
            webhookData.CounterAccountBankId,
            webhookData.CounterAccountBankName,
            webhookData.CounterAccountName,
            webhookData.CounterAccountNumber);
    }

    private static PayOsLinkStatus ToOrderStatus(PaymentLinkStatus status) => (PayOsLinkStatus)(int)status;

    private static TaxPercentage? ToPayOsTaxPercentage(PayOsTaxPercentage? taxPercentage) =>
        taxPercentage.HasValue ? (TaxPercentage)(int)taxPercentage.Value : null;

    private static PayOsLinkSnapshot MapLinkSnapshot(PaymentLink paymentLink) => new(
        ToOrderStatus(paymentLink.Status),
        paymentLink.Status.ToString(),
        paymentLink.Amount,
        paymentLink.AmountPaid,
        paymentLink.AmountRemaining,
        paymentLink.CreatedAt,
        paymentLink.CanceledAt,
        paymentLink.CancellationReason,
        paymentLink.Transactions?.Select(t => new PayOsTransactionSnapshot(
            t.Reference,
            t.Amount,
            t.AccountNumber,
            t.Description,
            t.TransactionDateTime,
            t.VirtualAccountName,
            t.VirtualAccountNumber,
            t.CounterAccountBankId,
            t.CounterAccountBankName,
            t.CounterAccountName,
            t.CounterAccountNumber)).ToList());
}
