using System;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Auth;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    internal static class AuthIdentityHelpers
    {
        internal static string CreateSecurityStamp()
        {
            return Guid.NewGuid().ToString("N");
        }

        internal static string GetOrCreateSecurityStamp(string? currentSecurityStamp)
        {
            return string.IsNullOrWhiteSpace(currentSecurityStamp)
                ? CreateSecurityStamp()
                : currentSecurityStamp;
        }

        internal static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        internal static bool IsEmailIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            return identifier.Contains('@');
        }

        internal static string NormalizePhone(string phone)
        {
            return phone.Trim();
        }

        internal static string? NormalizeOptionalPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }

            return phone.Trim();
        }

        internal static string ResolveLoginIdentifier(LoginRequestDto request)
        {
            if (!string.IsNullOrWhiteSpace(request.Identifier))
            {
                return request.Identifier.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                return request.Email.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                return request.Phone.Trim();
            }

            return string.Empty;
        }

        internal static bool VerifyPassword(
            IPasswordHasher passwordHasher,
            string inputPassword,
            string? storedPasswordHash,
            out bool needsRehash)
        {
            needsRehash = false;

            if (string.IsNullOrWhiteSpace(inputPassword) || string.IsNullOrWhiteSpace(storedPasswordHash))
            {
                return false;
            }

            if (IsBcryptHash(storedPasswordHash))
            {
                try
                {
                    return passwordHasher.VerifyPassword(inputPassword, storedPasswordHash);
                }
                catch
                {
                    return false;
                }
            }

            var matchedLegacyPlainText = string.Equals(inputPassword, storedPasswordHash, StringComparison.Ordinal);
            needsRehash = matchedLegacyPlainText;
            return matchedLegacyPlainText;
        }

        private static bool IsBcryptHash(string value)
        {
            return value.StartsWith("$2a$", StringComparison.Ordinal) ||
                   value.StartsWith("$2b$", StringComparison.Ordinal) ||
                   value.StartsWith("$2y$", StringComparison.Ordinal);
        }

        internal static void ValidateCustomerAccountForLogin(Customer customer)
        {
            if (customer.IsDeleted == true || customer.IsActive == false)
            {
                throw new UnauthorizedAccessException("Account is inactive.");
            }

            if (customer.IsLockedOut == true && customer.LockoutEnd.HasValue && customer.LockoutEnd > DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Account is temporarily locked.");
            }
        }

        internal static void ValidateStaffAccountForLogin(Staff staff)
        {
            var normalizedStatus = (staff.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (staff.IsDeleted == true || !string.Equals(normalizedStatus, nameof(StaffStatusCode.ACTIVE), StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Account is inactive.");
            }

            if (staff.IsLockedOut == true && staff.LockoutEnd.HasValue && staff.LockoutEnd > DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Account is temporarily locked.");
            }
        }

        internal static async Task EnsurePhoneAvailableAsync(
            IAuthCustomerAccountStore customerStore,
            IAuthStaffAccountStore staffStore,
            string phone,
            long? excludingStaffId = null,
            long? excludingCustomerId = null)
        {
            var customerPhoneTaken = await customerStore.IsCustomerPhoneTakenAsync(phone, excludingCustomerId);
            if (customerPhoneTaken)
            {
                throw new AuthRequestException("Số điện thoại đã được sử dụng bởi tài khoản khách hàng khác.");
            }

            var staffPhoneTaken = await staffStore.IsStaffPhoneTakenAsync(phone, excludingStaffId);
            if (staffPhoneTaken)
            {
                throw new AuthRequestException("Số điện thoại đã được sử dụng bởi tài khoản nhân viên khác.");
            }
        }
    }
}
