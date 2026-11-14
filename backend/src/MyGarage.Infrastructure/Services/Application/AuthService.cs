using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyGarage.Data;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class AuthService : Interfaces.IAuthService
    {
        private readonly IGarageContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ISystemClock _clock;
        private readonly IAuthCredentialRecoveryValidator _credentialRecoveryValidator;
        private readonly IAuthRateLimitHook _authRateLimitHook;
        private readonly ITokenBlacklistService? _tokenBlacklistService;
        private readonly IAuthSecurityStampValidator? _securityStampValidator;

        public AuthService(
            IGarageContext context,
            IConfiguration configuration,
            IEmailService emailService,
            ISystemClock clock,
            IAuthCredentialRecoveryValidator credentialRecoveryValidator,
            IAuthRateLimitHook authRateLimitHook,
            ITokenBlacklistService? tokenBlacklistService = null,
            IAuthSecurityStampValidator? securityStampValidator = null)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _clock = clock;
            _credentialRecoveryValidator = credentialRecoveryValidator;
            _authRateLimitHook = authRateLimitHook;
            _tokenBlacklistService = tokenBlacklistService;
            _securityStampValidator = securityStampValidator;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            var identifier = ResolveLoginIdentifier(request);
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new UnauthorizedAccessException("Invalid email/phone or password.");
            }

            var isEmailLogin = IsEmailIdentifier(identifier);
            var normalizedEmail = isEmailLogin ? NormalizeEmail(identifier) : string.Empty;
            var normalizedPhone = isEmailLogin ? string.Empty : NormalizePhone(identifier);

            var customer = isEmailLogin
                ? await _context.Customers.FirstOrDefaultAsync(c => c.Email == normalizedEmail)
                : await _context.Customers.FirstOrDefaultAsync(c => c.Phone == normalizedPhone);

            if (customer != null && VerifyPassword(request.Password, customer.PasswordHash, out var customerNeedsRehash))
            {
                ValidateCustomerAccountForLogin(customer);
                var nearestBranchId = await ResolveNearestBranchIdAsync(request.Lat, request.Lng);
                var nearestBranchName = await GetBranchNameAsync(nearestBranchId);
                AssignPreferredBranchToCustomer(customer, nearestBranchId);
                var customerSecurityStamp = GetOrCreateSecurityStamp(customer.SecurityStamp);

                if (customerNeedsRehash || !string.Equals(customer.SecurityStamp, customerSecurityStamp, StringComparison.Ordinal))
                {
                    customer.SecurityStamp = customerSecurityStamp;
                    if (customerNeedsRehash)
                    {
                        customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                    }
                }

                _context.Customers.Update(customer);
                await SaveChangesInTransactionAsync();

                return GenerateToken(
                    customer.Id.ToString(),
                    customer.FullName ?? "",
                    customer.Email ?? "",
                    customer.Phone ?? "",
                    "",
                    "Customer",
                    "CUSTOMER",
                    customerSecurityStamp,
                    "Customer",
                    nearestBranchId,
                    nearestBranchName);
            }

            var staff = isEmailLogin
                ? await _context.Staffs.FirstOrDefaultAsync(s => s.Email == normalizedEmail)
                : await _context.Staffs.FirstOrDefaultAsync(s => s.Phone == normalizedPhone);
            if (staff == null || !VerifyPassword(request.Password, staff.PasswordHash, out var staffNeedsRehash))
            {
                throw new UnauthorizedAccessException("Invalid email/phone or password.");
            }

            ValidateStaffAccountForLogin(staff);
            var staffSecurityStamp = GetOrCreateSecurityStamp(staff.SecurityStamp);

            if (staffNeedsRehash || !string.Equals(staff.SecurityStamp, staffSecurityStamp, StringComparison.Ordinal))
            {
                staff.SecurityStamp = staffSecurityStamp;
                if (staffNeedsRehash)
                {
                    staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                }

                _context.Staffs.Update(staff);
                await SaveChangesInTransactionAsync();
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

    }
}

