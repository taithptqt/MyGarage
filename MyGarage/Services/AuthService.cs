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
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class AuthService : Interfaces.IAuthService
    {
        private readonly GarageContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public AuthService(
            GarageContext context,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
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

                if (customerNeedsRehash)
                {
                    customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                }

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

            if (staffNeedsRehash)
            {
                staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
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
                roleName,
                staff.BranchId,
                staffBranchName);
        }

    }
}

