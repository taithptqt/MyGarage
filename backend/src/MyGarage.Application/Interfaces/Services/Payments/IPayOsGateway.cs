using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Interfaces;

namespace MyGarage.Application.Payments;

public sealed record PayOsInvoiceFileResult(
    byte[] Content,
    string ContentType,
    string FileName);

public interface IPayOsGateway
{
    Task<PayOsCreateLinkOutcome> CreatePaymentLinkAsync(
        OrderCreateRequest request,
        PreparedOrderPaymentContext context,
        CancellationToken ct = default);

    Task<PayOsLinkSnapshot> GetPaymentLinkAsync(
        string paymentLinkId,
        CancellationToken ct = default);

    Task<PayOsLinkSnapshot> CancelPaymentLinkAsync(
        string paymentLinkId,
        string cancellationReason,
        CancellationToken ct = default);

    Task<IReadOnlyList<PayOsInvoiceSnapshot>> GetInvoicesAsync(
        string paymentLinkId,
        CancellationToken ct = default);

    Task<PayOsInvoiceFileResult> DownloadInvoiceAsync(
        string invoiceId,
        string paymentLinkId,
        CancellationToken ct = default);

    Task ConfirmWebhookAsync(
        string webhookUrl,
        CancellationToken ct = default);

    Task<VerifiedPaymentWebhookInput> VerifyWebhookAsync(
        JsonElement webhookPayload,
        CancellationToken ct = default);
}
