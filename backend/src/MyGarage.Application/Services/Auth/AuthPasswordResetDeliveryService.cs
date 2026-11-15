using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Auth;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class AuthPasswordResetDeliveryService : IAuthPasswordResetDeliveryService
    {
        private readonly IAuthPasswordResetStore _passwordResetStore;
        private readonly IAuthConfigProvider _authConfigProvider;
        private readonly IEmailService _emailService;
        private readonly ISystemClock _clock;

        public AuthPasswordResetDeliveryService(
            IAuthPasswordResetStore passwordResetStore,
            IAuthConfigProvider authConfigProvider,
            IEmailService emailService,
            ISystemClock clock)
        {
            _passwordResetStore = passwordResetStore;
            _authConfigProvider = authConfigProvider;
            _emailService = emailService;
            _clock = clock;
        }

        public string ComputeTokenHash(string token)
        {
            return ComputePasswordResetTokenHash(token);
        }

        public async Task IssueAndSendResetTokenAsync(long? customerId, long? staffId, string normalizedEmail)
        {
            var now = _clock.UtcNow;
            await _passwordResetStore.RevokeOutstandingPasswordResetTokensAsync(
                customerId,
                staffId,
                now,
                exceptTokenId: null);

            var rawToken = GeneratePasswordResetToken();
            var token = new PasswordResetToken
            {
                CustomerId = customerId,
                StaffId = staffId,
                TokenHash = ComputePasswordResetTokenHash(rawToken),
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(_authConfigProvider.GetPasswordResetTokenExpiryMinutes())
            };

            await _passwordResetStore.AddPasswordResetTokenInTransactionAsync(token);

            var resetUrl = BuildPasswordResetUrl(rawToken);
            var emailBody =
                "<h3>Password Reset Request</h3>" +
                "<p>If you requested a password reset, use the link below. The link will expire soon.</p>" +
                $"<p><a href=\"{resetUrl}\">Reset your password</a></p>" +
                "<p>If you did not request this, you can ignore this email.</p>";

            try
            {
                await _emailService.SendEmailAsync(normalizedEmail, "MyGarage - Reset Password", emailBody);
            }
            catch
            {
                // Do not reveal account existence or fail password-reset requests on transient SMTP delivery errors.
            }
        }

        public async Task<(PasswordResetToken Token, DateTime UtcNow)> GetValidResetTokenAsync(string tokenHash)
        {
            var now = _clock.UtcNow;
            var token = await _passwordResetStore.GetPasswordResetTokenWithUsersAsync(tokenHash);

            if (token == null ||
                token.UsedAt.HasValue ||
                token.RevokedAt.HasValue ||
                token.ExpiresAt <= now)
            {
                throw new UnauthorizedAccessException("Invalid or expired reset token.");
            }

            return (token, now);
        }

        public async Task ConsumeResetTokenAsync(PasswordResetToken token, DateTime utcNow)
        {
            var customerId = token.Customer?.Id;
            var staffId = token.Staff?.Id;

            await _passwordResetStore.RevokeOutstandingPasswordResetTokensAsync(
                customerId,
                staffId,
                utcNow,
                token.Id);

            token.UsedAt = utcNow;
            await _passwordResetStore.SavePasswordResetChangesInTransactionAsync(token.Customer, token.Staff, token);
        }

        internal static string ComputePasswordResetTokenHash(string token)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private string BuildPasswordResetUrl(string rawToken)
        {
            var baseUrl = _authConfigProvider.GetPasswordResetUrlBase();
            var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{baseUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
        }

        private static string GeneratePasswordResetToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
