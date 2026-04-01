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
                && await _context.Customers.AnyAsync(c => c.Email == normalizedEmail))
            {
                throw new InvalidOperationException("Email already exists.");
            }

            var customer = new Customer
            {
                FullName = request.FullName,
                Email = normalizedEmail,
                Phone = request.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            var nearestBranchId = await ResolveNearestBranchIdAsync(request.Lat, request.Lng, request.City);
            var nearestBranchName = await GetBranchNameAsync(nearestBranchId);
            AssignPreferredBranchToCustomer(customer, nearestBranchId);
            AssignCityToCustomer(customer, request.City);
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();

            return GenerateToken(
                customer.Id.ToString(),
                customer.FullName ?? "",
                customer.Email ?? "",
                "Customer",
                "CUSTOMER",
                "Customer",
                nearestBranchId,
                nearestBranchName);
        }
    }
}