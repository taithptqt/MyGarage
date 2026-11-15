using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AuthService
    {
        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            var normalizedEmail = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : NormalizeEmail(request.Email);

            if (!string.IsNullOrWhiteSpace(normalizedEmail)
                && (await _context.Customers.AnyAsync(c => c.Email == normalizedEmail)
                    || await _context.Staffs.AnyAsync(s => s.Email == normalizedEmail)))
            {
                throw new InvalidOperationException("Email already exists.");
            }

            var normalizedPhone = NormalizeOptionalPhone(request.Phone);
            if (!string.IsNullOrWhiteSpace(normalizedPhone))
            {
                await EnsurePhoneAvailableAsync(normalizedPhone);
            }

            var customer = new Customer
            {
                FullName = request.FullName,
                Email = normalizedEmail,
                Phone = normalizedPhone ?? request.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                SecurityStamp = CreateSecurityStamp(),
                CreatedAt = _clock.UtcNow,
                UpdatedAt = _clock.UtcNow,
                IsActive = true
            };

            _context.Customers.Add(customer);
            await SaveChangesInTransactionAsync();

            var nearestBranchId = await ResolveNearestBranchIdAsync(request.Lat, request.Lng, request.City);
            var nearestBranchName = await GetBranchNameAsync(nearestBranchId);
            AssignPreferredBranchToCustomer(customer, nearestBranchId);
            AssignCityToCustomer(customer, request.City);
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
                customer.SecurityStamp ?? string.Empty,
                "Customer",
                nearestBranchId,
                nearestBranchName);
        }
    }
}
