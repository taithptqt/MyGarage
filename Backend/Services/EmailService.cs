using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var smtpServer = emailSettings["SmtpServer"] ?? throw new InvalidOperationException("SMTP Server not configured");
            var smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
            var senderName = emailSettings["SenderName"] ?? "MyGarage";
            var senderEmail = emailSettings["SenderEmail"] ?? throw new InvalidOperationException("Sender Email not configured");
            var password = emailSettings["Password"] ?? throw new InvalidOperationException("SMTP Password not configured");

            if (senderEmail == "your-email@gmail.com" || senderEmail == "string")
            {
                var enableMockEmailLogs =
                    bool.TryParse(emailSettings["EnableMockEmailLogs"], out var parsed) && parsed;

                if (enableMockEmailLogs)
                {
                    Console.WriteLine($"[MOCK EMAIL BYPASS] Sending email to {toEmail}");
                    Console.WriteLine($"Subject: {subject}");
                }
                return;
            }

            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail));
            emailMessage.To.Add(new MailboxAddress("", toEmail));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(senderEmail, password);
                await client.SendAsync(emailMessage);
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }
    }
}
