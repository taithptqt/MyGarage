using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var smtpServer = emailSettings["SmtpServer"];
            var smtpPort   = int.Parse(emailSettings["SmtpPort"] ?? "587");
            var senderName  = emailSettings["SenderName"] ?? "MyGarage";
            var senderEmail = emailSettings["SenderEmail"];
            var passwordRaw = emailSettings["Password"];
            var password = passwordRaw?.Trim().Replace(" ", string.Empty);

            // Guard: fail fast nếu config chưa được set
            if (string.IsNullOrWhiteSpace(smtpServer) ||
                string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(password)   ||
                senderEmail.Contains("your-email")    ||
                password.Contains("your-app-password"))
            {
                _logger.LogWarning(
                    "[EmailService] EmailSettings chưa được cấu hình đầy đủ. " +
                    "Email tới '{ToEmail}' bị bỏ qua. Kiểm tra appsettings.Development.json.", toEmail);
                return;
            }

            _logger.LogInformation("[EmailService] Đang gửi email tới {ToEmail} | Subject: {Subject}", toEmail, subject);

            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail));
            emailMessage.To.Add(new MailboxAddress("", toEmail));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = body,
                ContentTransferEncoding = ContentEncoding.QuotedPrintable
            };
            emailMessage.Body.ContentType.Charset = "utf-8";

            using var client = new SmtpClient();
            try
            {
                client.Timeout = 10000;
                await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(senderEmail, password);
                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);
                _logger.LogInformation("[EmailService] Gửi email thành công tới {ToEmail}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EmailService] Gửi email thất bại tới {ToEmail}", toEmail);
                throw; // Re-throw để caller có thể bắt nếu cần
            }
        }
    }
}
