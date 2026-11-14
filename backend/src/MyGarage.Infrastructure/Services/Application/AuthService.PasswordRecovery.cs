using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AuthService
    {
        public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            _credentialRecoveryValidator.ValidateForgotPassword(request);

            var normalizedEmail = NormalizeEmail(request.Email);
            var allowed = await _authRateLimitHook.CanRequestPasswordResetAsync(normalizedEmail);
            if (!allowed)
            {
                return;
            }

            var target = await ResolvePasswordRecoveryTargetAsync(normalizedEmail);
            if (target == null)
            {
                return;
            }

            var now = _clock.UtcNow;
            var resolvedTarget = target.Value;
            await RevokeOutstandingPasswordResetTokensAsync(resolvedTarget, now);

            var rawToken = GeneratePasswordResetToken();
            var token = new PasswordResetToken
            {
                CustomerId = resolvedTarget.CustomerId,
                StaffId = resolvedTarget.StaffId,
                TokenHash = ComputePasswordResetTokenHash(rawToken),
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(GetPasswordResetTokenExpiryMinutes())
            };

            _context.PasswordResetTokens.Add(token);
            await SaveChangesInTransactionAsync();

            var resetUrl = BuildPasswordResetUrl(rawToken);
            var emailBody =
                "<h3>Password Reset Request</h3>" +
                "<p>If you requested a password reset, use the link below. The link will expire soon.</p>" +
                $"<p><a href=\"{resetUrl}\">Reset your password</a></p>" +
                "<p>If you did not request this, you can ignore this email.</p>";

            await _emailService.SendEmailAsync(normalizedEmail, "MyGarage - Reset Password", emailBody);
        }

        public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            _credentialRecoveryValidator.ValidateResetPassword(request);

            var tokenHash = ComputePasswordResetTokenHash(request.Token);
            var allowed = await _authRateLimitHook.CanSubmitPasswordResetAsync(tokenHash);
            if (!allowed)
            {
                throw new UnauthorizedAccessException("Invalid or expired reset token.");
            }

            var now = _clock.UtcNow;
            var token = await _context.PasswordResetTokens
                .Include(x => x.Customer)
                .Include(x => x.Staff)
                .SingleOrDefaultAsync(x => x.TokenHash == tokenHash);

            if (token == null ||
                token.UsedAt.HasValue ||
                token.RevokedAt.HasValue ||
                token.ExpiresAt <= now)
            {
                throw new UnauthorizedAccessException("Invalid or expired reset token.");
            }

            if (token.Customer != null)
            {
                if (!CanRecoverPassword(token.Customer))
                {
                    throw new UnauthorizedAccessException("Invalid or expired reset token.");
                }

                token.Customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                token.Customer.SecurityStamp = CreateSecurityStamp();
                ResetCustomerLockout(token.Customer);
                _context.Customers.Update(token.Customer);
                await RevokeOutstandingPasswordResetTokensAsync(
                    new PasswordRecoveryTarget(token.Customer.Id, null),
                    now,
                    token.Id);
            }
            else if (token.Staff != null)
            {
                if (!CanRecoverPassword(token.Staff))
                {
                    throw new UnauthorizedAccessException("Invalid or expired reset token.");
                }

                token.Staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                token.Staff.SecurityStamp = CreateSecurityStamp();
                ResetStaffLockout(token.Staff);
                _context.Staffs.Update(token.Staff);
                await RevokeOutstandingPasswordResetTokensAsync(
                    new PasswordRecoveryTarget(null, token.Staff.Id),
                    now,
                    token.Id);
            }
            else
            {
                throw new UnauthorizedAccessException("Invalid or expired reset token.");
            }

            token.UsedAt = now;
            await SaveChangesInTransactionAsync();
            if (token.Customer != null)
            {
                _tokenBlacklistService?.RevokeUserSessions("Customer", token.Customer.Id);
                _securityStampValidator?.Invalidate("Customer", token.Customer.Id);
            }
            else if (token.Staff != null)
            {
                _tokenBlacklistService?.RevokeUserSessions("Staff", token.Staff.Id);
                _securityStampValidator?.Invalidate("Staff", token.Staff.Id);
            }
        }

        private async Task<PasswordRecoveryTarget?> ResolvePasswordRecoveryTargetAsync(string normalizedEmail)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(x => x.Email == normalizedEmail);
            if (customer != null && CanRecoverPassword(customer))
            {
                return new PasswordRecoveryTarget(customer.Id, null);
            }

            var staff = await _context.Staffs
                .FirstOrDefaultAsync(x => x.Email == normalizedEmail);
            if (staff != null && CanRecoverPassword(staff))
            {
                return new PasswordRecoveryTarget(null, staff.Id);
            }

            return null;
        }

        private async Task RevokeOutstandingPasswordResetTokensAsync(
            PasswordRecoveryTarget target,
            DateTime utcNow,
            long? exceptTokenId = null)
        {
            IQueryable<PasswordResetToken> query = _context.PasswordResetTokens
                .Where(x => !x.UsedAt.HasValue && !x.RevokedAt.HasValue && x.ExpiresAt > utcNow);

            if (target.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == target.CustomerId.Value);
            }
            else if (target.StaffId.HasValue)
            {
                query = query.Where(x => x.StaffId == target.StaffId.Value);
            }

            if (exceptTokenId.HasValue)
            {
                query = query.Where(x => x.Id != exceptTokenId.Value);
            }

            var tokens = await query.ToListAsync();
            foreach (var token in tokens)
            {
                token.RevokedAt = utcNow;
            }
        }

        private int GetPasswordResetTokenExpiryMinutes()
        {
            var configured = _configuration["Auth:PasswordResetTokenExpiryMinutes"];
            return int.TryParse(configured, out var minutes) && minutes > 0
                ? minutes
                : 15;
        }

        private string BuildPasswordResetUrl(string rawToken)
        {
            var baseUrl = (_configuration["Auth:PasswordResetUrlBase"] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ValidationException("Password reset URL is not configured.");
            }

            var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{baseUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
        }

        private static bool CanRecoverPassword(Customer customer)
        {
            return customer.IsDeleted != true && customer.IsActive != false;
        }

        private static bool CanRecoverPassword(Staff staff)
        {
            var normalizedStatus = (staff.Status ?? string.Empty).Trim().ToUpperInvariant();
            var isInactiveStatus =
                normalizedStatus == "TERMINATED" ||
                normalizedStatus == "SUSPENDED" ||
                normalizedStatus.Contains("INACTIVE", StringComparison.Ordinal);

            return staff.IsDeleted != true && !isInactiveStatus;
        }

        private static void ResetCustomerLockout(Customer customer)
        {
            customer.AccessFailedCount = 0;
            customer.IsLockedOut = false;
            customer.LockoutEnd = null;
        }

        private static void ResetStaffLockout(Staff staff)
        {
            staff.AccessFailedCount = 0;
            staff.IsLockedOut = false;
            staff.LockoutEnd = null;
        }

        private static string GeneratePasswordResetToken()
        {
            return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        }

        private static string ComputePasswordResetTokenHash(string token)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static string ComputePasswordResetTokenHashForTests(string token)
        {
            return ComputePasswordResetTokenHash(token);
        }

        private readonly record struct PasswordRecoveryTarget(long? CustomerId, long? StaffId);
    }
}
