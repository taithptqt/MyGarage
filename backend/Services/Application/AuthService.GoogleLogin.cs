using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AuthService
    {
        public async Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginRequestDto request)
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new List<string> { _configuration["GoogleAuth:ClientId"] ?? throw new InvalidOperationException("Google ClientId missing") }
            };

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("Invalid Google Token.", ex);
            }

            var normalizedEmail = NormalizeEmail(payload.Email);

            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.Email == normalizedEmail);
            if (staff != null)
            {
                ValidateStaffAccountForLogin(staff);
                var staffSecurityStamp = GetOrCreateSecurityStamp(staff.SecurityStamp);
                if (!string.Equals(staff.SecurityStamp, staffSecurityStamp, StringComparison.Ordinal))
                {
                    staff.SecurityStamp = staffSecurityStamp;
                    _context.Staffs.Update(staff);
                    await _context.SaveChangesAsync();
                }

                var (roleCode, roleName) = await GetStaffRoleInfoAsync(staff.RoleId);
                var staffBranchName = await GetBranchNameAsync(staff.BranchId);
                return GenerateToken(
                    staff.Id.ToString(),
                    staff.FullName ?? "",
                    staff.Email ?? "",
                    staff.Phone ?? "",
                    staff.AvatarUrl ?? "",
                    "Staff",
                    roleCode,
                    staffSecurityStamp,
                    roleName,
                    staff.BranchId,
                    staffBranchName);
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == normalizedEmail);
            if (customer == null)
            {
                customer = new Customer
                {
                    Email = normalizedEmail,
                    FullName = payload.Name ?? "",
                    Phone = "",
                    PasswordHash = "",
                    SecurityStamp = CreateSecurityStamp(),
                    CreatedAt = _clock.UtcNow,
                    UpdatedAt = _clock.UtcNow,
                    IsActive = true,
                    IsDeleted = false
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            ValidateCustomerAccountForLogin(customer);
            var nearestBranchId = await ResolveNearestBranchIdAsync(request.Lat, request.Lng);
            var nearestBranchName = await GetBranchNameAsync(nearestBranchId);
            AssignPreferredBranchToCustomer(customer, nearestBranchId);
            customer.SecurityStamp = GetOrCreateSecurityStamp(customer.SecurityStamp);
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
            return GenerateToken(
                customer.Id.ToString(),
                customer.FullName ?? "",
                customer.Email ?? "",
                customer.Phone ?? "",
                "",
                "Customer",
                "CUSTOMER",
                customer.SecurityStamp ?? string.Empty,
                "Customer",
                nearestBranchId,
                nearestBranchName);
        }
    }
}
