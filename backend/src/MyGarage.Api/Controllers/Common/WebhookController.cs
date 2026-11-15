using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Payments;

namespace MyGarage.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public class WebhookController(
    IPaymentWebhookService paymentWebhookService) : ControllerBase
{
    private readonly IPaymentWebhookService _paymentWebhookService = paymentWebhookService;

    [AllowAnonymous]
    [HttpPost("payment")]
    public async Task<ActionResult> VerifyPayment([FromBody] JsonElement webhookPayload)
    {
        if (webhookPayload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return this.BadRequestProblem("Webhook data is required");
        }

        try
        {
            var result = await _paymentWebhookService.VerifyAndProcessWebhookAsync(webhookPayload, HttpContext.RequestAborted);
            if (result.IsTestWebhook)
            {
                return Ok(new { message = "Webhook processed successfully" });
            }

            return Ok(new { message = "Webhook processed successfully", orderCode = result.OrderCode });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Webhook processing error: {ex.Message}");
            if (ex.Message.Contains("Invalid signature", StringComparison.OrdinalIgnoreCase))
            {
                return this.BadRequestProblem("Invalid webhook signature");
            }
            return Problem(ex.Message);
        }
    }
}
