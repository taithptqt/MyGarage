using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AuthService
    {
        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        private static bool IsEmailIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            return identifier.Contains('@');
        }

        private static string NormalizePhone(string phone)
        {
            return phone.Trim();
        }

        private static string ResolveLoginIdentifier(LoginRequestDto request)
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


        private static bool VerifyPassword(string inputPassword, string? storedPasswordHash, out bool needsRehash)
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
                    return BCrypt.Net.BCrypt.Verify(inputPassword, storedPasswordHash);
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

        private async Task<(string RoleCode, string RoleName)> GetStaffRoleInfoAsync(long? roleId)
        {
            if (!roleId.HasValue)
            {
                return ("STAFF", "Staff");
            }

            var role = await _context.Roles
                .Where(r => r.Id == roleId.Value)
                .Select(r => new { r.RoleCode, r.RoleName })
                .FirstOrDefaultAsync();

            if (role == null)
            {
                return ("STAFF", "Staff");
            }

            return (
                string.IsNullOrWhiteSpace(role.RoleCode) ? "STAFF" : role.RoleCode.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(role.RoleName) ? "Staff" : role.RoleName.Trim());
        }

        private static void ValidateCustomerAccountForLogin(Customer customer)
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

        private static void ValidateStaffAccountForLogin(Staff staff)
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
    }
}

