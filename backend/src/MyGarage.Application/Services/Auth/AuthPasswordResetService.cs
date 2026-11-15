using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public sealed class AuthPasswordResetService : IAuthPasswordResetService
    {
        private readonly IAuthAccountCredentialService _accountCredentialService;
        private readonly IAuthPasswordResetDeliveryService _passwordResetDeliveryService;
        private readonly IAuthCredentialRecoveryValidator _credentialRecoveryValidator;
        private readonly IAuthRateLimitHook _authRateLimitHook;

        public AuthPasswordResetService(
            IAuthAccountCredentialService accountCredentialService,
            IAuthPasswordResetDeliveryService passwordResetDeliveryService,
            IAuthCredentialRecoveryValidator credentialRecoveryValidator,
            IAuthRateLimitHook authRateLimitHook)
        {
            _accountCredentialService = accountCredentialService;
            _passwordResetDeliveryService = passwordResetDeliveryService;
            _credentialRecoveryValidator = credentialRecoveryValidator;
            _authRateLimitHook = authRateLimitHook;
        }

        public Task ChangePasswordAsync(long userId, string role, ChangePasswordRequestDto request)
        {
            return _accountCredentialService.ChangePasswordAsync(userId, role, request.OldPassword, request.NewPassword);
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            _credentialRecoveryValidator.ValidateForgotPassword(request);

            var normalizedEmail = AuthIdentityHelpers.NormalizeEmail(request.Email);
            var allowed = await _authRateLimitHook.CanRequestPasswordResetAsync(normalizedEmail);
            if (!allowed)
            {
                return;
            }

            var target = await _accountCredentialService.ResolveRecoveryTargetAsync(normalizedEmail);
            if (target == null)
            {
                return;
            }

            await _passwordResetDeliveryService.IssueAndSendResetTokenAsync(
                target.Value.CustomerId,
                target.Value.StaffId,
                normalizedEmail);
        }

        public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            _credentialRecoveryValidator.ValidateResetPassword(request);

            var tokenHash = _passwordResetDeliveryService.ComputeTokenHash(request.Token);
            var allowed = await _authRateLimitHook.CanSubmitPasswordResetAsync(tokenHash);
            if (!allowed)
            {
                throw new UnauthorizedAccessException("Invalid or expired reset token.");
            }

            var (token, now) = await _passwordResetDeliveryService.GetValidResetTokenAsync(tokenHash);
            var (role, userId) = _accountCredentialService.PreparePasswordReset(token, request.NewPassword);
            await _passwordResetDeliveryService.ConsumeResetTokenAsync(token, now);
            _accountCredentialService.InvalidateUserSessions(role, userId);
        }

        public static string ComputePasswordResetTokenHashForTests(string token)
        {
            return AuthPasswordResetDeliveryService.ComputePasswordResetTokenHash(token);
        }
    }
}
