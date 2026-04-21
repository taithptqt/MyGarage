using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AuthService
    {
        private const string PhoneRequestCode = "PHONE_REQUEST";
        private static readonly Regex GooglePhoneRegex = new(@"^(0\d{9}|\+84\d{9})$", RegexOptions.Compiled);

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

            if (string.IsNullOrWhiteSpace(payload.Email))
            {
                throw new UnauthorizedAccessException("Google account does not provide email.");
            }

            if (string.IsNullOrWhiteSpace(payload.Subject))
            {
                throw new UnauthorizedAccessException("Google account identifier is missing.");
            }

            var normalizedEmail = NormalizeEmail(payload.Email);
            var googleId = payload.Subject.Trim();
            var normalizedPhone = NormalizeOptionalPhone(request.Phone);

            if (!string.IsNullOrWhiteSpace(normalizedPhone) && !GooglePhoneRegex.IsMatch(normalizedPhone))
            {
                throw new InvalidOperationException("Số điện thoại không đúng định dạng (0xxxxxxxxx hoặc +84xxxxxxxxx).");
            }

            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.GoogleId == googleId);
            if (staff == null)
            {
                staff = await _context.Staffs.FirstOrDefaultAsync(s => s.Email == normalizedEmail);
            }

            if (staff != null)
            {
                ValidateStaffAccountForLogin(staff);
                var nowUtc = DateTime.UtcNow;
                var shouldSaveStaff = false;
                var staffSecurityStamp = GetOrCreateSecurityStamp(staff.SecurityStamp);

                if (!string.Equals(staff.SecurityStamp, staffSecurityStamp, StringComparison.Ordinal))
                {
                    staff.SecurityStamp = staffSecurityStamp;
                    shouldSaveStaff = true;
                }

                if (!string.Equals(staff.GoogleId, googleId, StringComparison.Ordinal))
                {
                    staff.GoogleId = googleId;
                    shouldSaveStaff = true;
                }

                if (string.IsNullOrWhiteSpace(staff.Phone))
                {
                    if (string.IsNullOrWhiteSpace(normalizedPhone))
                    {
                        if (shouldSaveStaff)
                        {
                            staff.UpdatedAt = nowUtc;
                            _context.Staffs.Update(staff);
                            await SaveChangesInTransactionAsync();
                        }

                        throw new UnauthorizedAccessException($"{PhoneRequestCode}:STAFF");
                    }

                    await EnsurePhoneAvailableAsync(normalizedPhone, excludingStaffId: staff.Id);
                    staff.Phone = normalizedPhone;
                    shouldSaveStaff = true;
                }

                if (shouldSaveStaff)
                {
                    staff.UpdatedAt = nowUtc;
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

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.GoogleId == googleId);
            if (customer == null)
            {
                customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == normalizedEmail);
            }
            if (customer == null)
            {
                customer = new Customer
                {
                    Email = normalizedEmail,
                    GoogleId = googleId,
                    FullName = payload.Name ?? "",
                    Phone = normalizedPhone,
                    PasswordHash = "",
                    SecurityStamp = CreateSecurityStamp(),
                    CreatedAt = _clock.UtcNow,
                    UpdatedAt = _clock.UtcNow,
                    IsActive = true,
                    IsDeleted = false
                };

                _context.Customers.Add(customer);
                await SaveChangesInTransactionAsync();
            }
            else if (!string.Equals(customer.GoogleId, googleId, StringComparison.Ordinal))
            {
                customer.GoogleId = googleId;
                customer.UpdatedAt = DateTime.UtcNow;
                _context.Customers.Update(customer);
                await SaveChangesInTransactionAsync();
            }

            ValidateCustomerAccountForLogin(customer);

            if (string.IsNullOrWhiteSpace(customer.Phone))
            {
                if (string.IsNullOrWhiteSpace(normalizedPhone))
                {
                    throw new UnauthorizedAccessException($"{PhoneRequestCode}:CUSTOMER");
                }

                await EnsurePhoneAvailableAsync(normalizedPhone, excludingCustomerId: customer.Id);
                customer.Phone = normalizedPhone;
                customer.UpdatedAt = DateTime.UtcNow;
            }

            var nearestBranchId = await ResolveNearestBranchIdAsync(request.Lat, request.Lng);
            var nearestBranchName = await GetBranchNameAsync(nearestBranchId);
            AssignPreferredBranchToCustomer(customer, nearestBranchId);
            customer.SecurityStamp = GetOrCreateSecurityStamp(customer.SecurityStamp);
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

        private static string? NormalizeOptionalPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }

            return phone.Trim();
        }

        private async Task EnsurePhoneAvailableAsync(string phone, long? excludingStaffId = null, long? excludingCustomerId = null)
        {
            var customerPhoneTaken = await _context.Customers.AnyAsync(c =>
                c.Phone == phone &&
                (!excludingCustomerId.HasValue || c.Id != excludingCustomerId.Value));

            if (customerPhoneTaken)
            {
                throw new InvalidOperationException("Số điện thoại đã được sử dụng bởi tài khoản khách hàng khác.");
            }

            var staffPhoneTaken = await _context.Staffs.AnyAsync(s =>
                s.Phone == phone &&
                (!excludingStaffId.HasValue || s.Id != excludingStaffId.Value));

            if (staffPhoneTaken)
            {
                throw new InvalidOperationException("Số điện thoại đã được sử dụng bởi tài khoản nhân viên khác.");
            }
        }
        private async Task SaveChangesInTransactionAsync()
        {
            await ExecuteInTransactionAsync(async () =>
            {
                await _context.SaveChangesAsync();
            });
        }

        private async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await TryBeginTransactionAsync();
                await action();
                if (tx != null)
                {
                    await tx.CommitAsync();
                }
            });
        }

        private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
        {
            if (_context.Database.CurrentTransaction != null)
            {
                return null;
            }

            return await _context.Database.BeginTransactionAsync();
        }
    }
}
