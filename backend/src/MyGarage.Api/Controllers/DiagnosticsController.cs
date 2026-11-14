using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

/// <summary>
/// Endpoint chẩn đoán hệ thống — CHỈ dùng trong môi trường Development.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminStaffOnly")]
[Route("api/diagnostics")]
[Route("api/v1/diagnostics")]
public class DiagnosticsController(
    IEmailService emailService,
    IPaymentConfirmationService paymentConfirmationService,
    IGarageContext db,
    IHostEnvironment environment) : ControllerBase
{
    private readonly IEmailService _emailService = emailService;
    private readonly IPaymentConfirmationService _paymentConfirmationService = paymentConfirmationService;
    private readonly IGarageContext _db = db;
    private readonly IHostEnvironment _environment = environment;

    /// <summary>
    /// Test gửi SMTP email trực tiếp.
    /// GET /api/diagnostics/test-email?to=yourmail@gmail.com
    /// </summary>
    [HttpGet("test-email")]
    public async Task<IActionResult> TestEmail([FromQuery] string to = "test@example.com")
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }
        try
        {
            await _emailService.SendEmailAsync(
                to,
                "MyGarage SMTP Test — " + DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy"),
                $@"<div style='font-family:Arial;padding:20px;'>
                    <h2 style='color:#0056b3;'>✅ SMTP hoạt động!</h2>
                    <p>Email test gửi thành công từ <strong>MyGarage System</strong>.</p>
                    <p>Thời gian: <strong>{DateTime.Now:dd/MM/yyyy HH:mm:ss}</strong></p>
                    <p>Nếu bạn nhận được email này, cấu hình SMTP đã chính xác.</p>
                </div>"
            );

            return Ok(new
            {
                success = true,
                message = $"Email test đã gửi tới {to}. Kiểm tra hộp thư (và Spam).",
                sentAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Gửi email thất bại.",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// Trigger thủ công email cho một payment đã COMPLETED.
    /// POST /api/diagnostics/retry-payment-email/{paymentId}
    /// </summary>
    [HttpPost("retry-payment-email/{paymentId:long}")]
    public async Task<IActionResult> RetryPaymentEmail(long paymentId)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment is null)
            return NotFound(new { message = $"Không tìm thấy Payment #{paymentId}" });

        if (payment.Status != "COMPLETED")
            return BadRequest(new { message = $"Payment #{paymentId} chưa COMPLETED (status hiện tại: {payment.Status})" });

        try
        {
            // Gọi trực tiếp TrySendPaymentSuccessEmailAsync thông qua interface giả lập
            // bằng cách reset status về PENDING rồi gọi ApplyOutcomeAsync
            var previousStatus = payment.Status;
            payment.Status = "PENDING"; // Tạm đặt về PENDING để trigger email
            await _db.SaveChangesAsync();

            await _paymentConfirmationService.ApplyOutcomeAsync(payment, payment.Amount, "COMPLETED");

            return Ok(new
            {
                success = true,
                message = $"Đã retry gửi email cho Payment #{paymentId}.",
                paymentId,
                amount = payment.Amount
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Retry thất bại.",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Kiểm tra trạng thái config email.
    /// GET /api/diagnostics/email-config
    /// </summary>
    [HttpGet("email-config")]
    public IActionResult CheckEmailConfig([FromServices] IConfiguration config)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }
        var section = config.GetSection("EmailSettings");
        var senderEmail = section["SenderEmail"] ?? "(not set)";
        var smtpServer = section["SmtpServer"] ?? "(not set)";
        var smtpPort = section["SmtpPort"] ?? "(not set)";
        var hasPassword = !string.IsNullOrWhiteSpace(section["Password"]) && !(section["Password"]?.Contains("your-app-password") ?? true);

        return Ok(new
        {
            smtpServer,
            smtpPort,
            senderEmail,
            passwordConfigured = hasPassword,
            isPlaceholder = senderEmail.Contains("your-email"),
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "unknown"
        });
    }
}
