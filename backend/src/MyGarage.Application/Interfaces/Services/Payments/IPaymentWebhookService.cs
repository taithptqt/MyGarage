using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Payments;

public sealed record WebhookProcessResult(bool IsTestWebhook, long? OrderCode);

public sealed record VerifiedPaymentWebhookInput(
    long OrderCode,
    long Amount,
    string Reference,
    string AccountNumber,
    string Description,
    string TransactionDateTime,
    string? PaymentLinkId,
    string? VirtualAccountName,
    string? VirtualAccountNumber,
    string? CounterAccountBankId,
    string? CounterAccountBankName,
    string? CounterAccountName,
    string? CounterAccountNumber);

public interface IPaymentWebhookService
{
    Task<WebhookProcessResult> VerifyAndProcessWebhookAsync(JsonElement webhookPayload, CancellationToken ct = default);
    Task ProcessVerifiedPaymentAsync(VerifiedPaymentWebhookInput webhookData, CancellationToken ct = default);
}
