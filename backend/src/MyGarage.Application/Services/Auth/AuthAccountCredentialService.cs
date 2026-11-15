using System;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Auth;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class AuthAccountCredentialService : IAuthAccountCredentialService
    {
        private readonly IAuthCustomerAccountStore _customerStore;
        private readonly IAuthStaffAccountStore _staffStore;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenBlacklistService? _tokenBlacklistService;
        private readonly IAuthSecurityStampValidator? _securityStampValidator;

        public AuthAccountCredentialService(
            IAuthCustomerAccountStore customerStore,
            IAuthStaffAccountStore staffStore,
            IPasswordHasher passwordHasher,
            ITokenBlacklistService? tokenBlacklistService = null,
            IAuthSecurityStampValidator? securityStampValidator = null)
        {
            _customerStore = customerStore;
            _staffStore = staffStore;
            _passwordHasher = passwordHasher;
            _tokenBlacklistService = tokenBlacklistService;
            _securityStampValidator = securityStampValidator;
        }

        public async Task ChangePasswordAsync(long userId, string role, string oldPassword, string newPassword)
        {
            if (role == "Customer")
            {
                var customer = await _customerStore.GetCustomerByIdAsync(userId);
                if (customer == null) throw new UnauthorizedAccessException("User not found.");

                if (!AuthIdentityHelpers.VerifyPassword(_passwordHasher, oldPassword, customer.PasswordHash, out _))
                {
                    throw new UnauthorizedAccessException("Incorrect old password.");
                }

                ApplyCustomerCredentials(customer, newPassword);
                await _customerStore.UpdateCustomerInTransactionAsync(customer);
                InvalidateUserSessions("Customer", customer.Id);
            }
            else if (role == "Staff")
            {
                var staff = await _staffStore.GetStaffByIdAsync(userId);
                if (staff == null) throw new UnauthorizedAccessException("User not found.");

                if (!AuthIdentityHelpers.VerifyPassword(_passwordHasher, oldPassword, staff.PasswordHash, out _))
                {
                    throw new UnauthorizedAccessException("Incorrect old password.");
                }

                ApplyStaffCredentials(staff, newPassword);
                await _staffStore.UpdateStaffInTransactionAsync(staff);
                InvalidateUserSessions("Staff", staff.Id);
            }
        }

        public async Task<PasswordRecoveryTarget?> ResolveRecoveryTargetAsync(string normalizedEmail)
        {
            var customer = await _customerStore.GetCustomerByEmailAsync(normalizedEmail);
            if (customer != null && CanRecoverPassword(customer))
            {
                return new PasswordRecoveryTarget(customer.Id, null);
            }

            var staff = await _staffStore.GetStaffByEmailAsync(normalizedEmail);
            if (staff != null && CanRecoverPassword(staff))
            {
                return new PasswordRecoveryTarget(null, staff.Id);
            }

            return null;
        }

        public (string Role, long UserId) PreparePasswordReset(PasswordResetToken token, string newPassword)
        {
            if (token.Customer != null)
            {
                if (!CanRecoverPassword(token.Customer))
                {
                    throw new UnauthorizedAccessException("Invalid or expired reset token.");
                }

                ApplyCustomerCredentials(token.Customer, newPassword);
                return ("Customer", token.Customer.Id);
            }

            if (token.Staff != null)
            {
                if (!CanRecoverPassword(token.Staff))
                {
                    throw new UnauthorizedAccessException("Invalid or expired reset token.");
                }

                ApplyStaffCredentials(token.Staff, newPassword);
                return ("Staff", token.Staff.Id);
            }

            throw new UnauthorizedAccessException("Invalid or expired reset token.");
        }

        public void InvalidateUserSessions(string role, long userId)
        {
            _tokenBlacklistService?.RevokeUserSessions(role, userId);
            _securityStampValidator?.Invalidate(role, userId);
        }

        private void ApplyCustomerCredentials(Customer customer, string newPassword)
        {
            customer.PasswordHash = _passwordHasher.HashPassword(newPassword);
            customer.SecurityStamp = AuthIdentityHelpers.CreateSecurityStamp();
            customer.AccessFailedCount = 0;
            customer.IsLockedOut = false;
            customer.LockoutEnd = null;
        }

        private void ApplyStaffCredentials(Staff staff, string newPassword)
        {
            staff.PasswordHash = _passwordHasher.HashPassword(newPassword);
            staff.SecurityStamp = AuthIdentityHelpers.CreateSecurityStamp();
            staff.AccessFailedCount = 0;
            staff.IsLockedOut = false;
            staff.LockoutEnd = null;
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
    }
}
