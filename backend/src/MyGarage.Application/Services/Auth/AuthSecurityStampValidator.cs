using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Auth;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AuthSecurityStampValidator : IAuthSecurityStampValidator
    {
        private readonly IAuthStore _authStore;

        public AuthSecurityStampValidator(IAuthStore authStore)
        {
            _authStore = authStore;
        }

        public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = principal.FindFirst(ClaimTypes.Role)?.Value;
            var stampClaim = principal.FindFirst("security_stamp")?.Value;

            if (!long.TryParse(userIdClaim, out var userId) || string.IsNullOrWhiteSpace(role))
            {
                return false;
            }

            if (!string.Equals(role, "Customer", StringComparison.Ordinal) &&
                !string.Equals(role, "Staff", StringComparison.Ordinal))
            {
                return false;
            }

            if (role == "Customer")
            {
                var customer = await _authStore.GetCustomerSecurityStampAsync(userId, cancellationToken);
                return customer != null && IsStampValid(customer.SecurityStamp, stampClaim);
            }

            var staff = await _authStore.GetStaffSecurityStampAsync(userId, cancellationToken);
            return staff != null && string.Equals(staff.Status?.Trim(), "ACTIVE", StringComparison.OrdinalIgnoreCase)
                && IsStampValid(staff.SecurityStamp, stampClaim);
        }

        public void Invalidate(string role, long userId)
        {
            // Validation reads the current database value on every request.
        }

        private static bool IsStampValid(string? currentStamp, string? stampClaim)
        {
            if (string.IsNullOrWhiteSpace(currentStamp))
            {
                return string.IsNullOrWhiteSpace(stampClaim);
            }

            return string.Equals(currentStamp, stampClaim, StringComparison.Ordinal);
        }
    }
}
