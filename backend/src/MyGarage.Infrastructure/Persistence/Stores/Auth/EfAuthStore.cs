using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Interfaces.Stores.Auth;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Auth
{
    public sealed class EfAuthStore : IAuthStore
    {
        private readonly IGarageContext _context;

        public EfAuthStore(IGarageContext context)
        {
            _context = context;
        }

        public async Task<AuthCustomerStampSnapshot?> GetCustomerSecurityStampAsync(long customerId, CancellationToken cancellationToken = default)
        {
            return await _context.Customers.AsNoTracking()
                .Where(x => x.Id == customerId && x.IsDeleted != true && x.IsActive != false)
                .Select(x => new AuthCustomerStampSnapshot(x.SecurityStamp))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<AuthStaffStampSnapshot?> GetStaffSecurityStampAsync(long staffId, CancellationToken cancellationToken = default)
        {
            return await _context.Staffs.AsNoTracking()
                .Where(x => x.Id == staffId && x.IsDeleted != true)
                .Select(x => new AuthStaffStampSnapshot(x.SecurityStamp, x.Status))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Customer?> GetCustomerByIdAsync(long customerId)
        {
            return await _context.Customers.FindAsync(customerId);
        }

        public Task<Customer?> GetCustomerByEmailAsync(string normalizedEmail)
        {
            return _context.Customers.FirstOrDefaultAsync(c => c.Email == normalizedEmail);
        }

        public Task<Customer?> GetCustomerByPhoneAsync(string normalizedPhone)
        {
            return _context.Customers.FirstOrDefaultAsync(c => c.Phone == normalizedPhone);
        }

        public Task<Customer?> GetCustomerByGoogleIdAsync(string googleId)
        {
            return _context.Customers.FirstOrDefaultAsync(c => c.GoogleId == googleId);
        }

        public async Task<Staff?> GetStaffByIdAsync(long staffId)
        {
            return await _context.Staffs.FindAsync(staffId);
        }

        public Task<Staff?> GetStaffByEmailAsync(string normalizedEmail)
        {
            return _context.Staffs.FirstOrDefaultAsync(s => s.Email == normalizedEmail);
        }

        public Task<Staff?> GetStaffByPhoneAsync(string normalizedPhone)
        {
            return _context.Staffs.FirstOrDefaultAsync(s => s.Phone == normalizedPhone);
        }

        public Task<Staff?> GetStaffByGoogleIdAsync(string googleId)
        {
            return _context.Staffs.FirstOrDefaultAsync(s => s.GoogleId == googleId);
        }

        public async Task<bool> EmailExistsAsync(string normalizedEmail)
        {
            return await _context.Customers.AnyAsync(c => c.Email == normalizedEmail)
                || await _context.Staffs.AnyAsync(s => s.Email == normalizedEmail);
        }

        public Task<bool> IsCustomerPhoneTakenAsync(string phone, long? excludingCustomerId = null)
        {
            return _context.Customers.AnyAsync(c =>
                c.Phone == phone &&
                (!excludingCustomerId.HasValue || c.Id != excludingCustomerId.Value));
        }

        public Task<bool> IsStaffPhoneTakenAsync(string phone, long? excludingStaffId = null)
        {
            return _context.Staffs.AnyAsync(s =>
                s.Phone == phone &&
                (!excludingStaffId.HasValue || s.Id != excludingStaffId.Value));
        }

        public Task<List<AuthBranchGeoSnapshot>> GetActiveBranchesAsync()
        {
            return _context.Branches
                .Where(b => b.IsDeleted != true)
                .Select(b => new AuthBranchGeoSnapshot(b.Id, b.Name, b.Lat, b.Lng))
                .ToListAsync();
        }

        public async Task<string> GetBranchNameAsync(long? branchId)
        {
            if (!branchId.HasValue) return string.Empty;
            return await _context.Branches
                       .Where(b => b.Id == branchId.Value)
                       .Select(b => b.Name ?? string.Empty)
                       .FirstOrDefaultAsync()
                   ?? string.Empty;
        }

        public async Task<(string RoleCode, string RoleName)> GetStaffRoleInfoAsync(long? roleId)
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

        public async Task AddCustomerInTransactionAsync(Customer customer)
        {
            _context.Customers.Add(customer);
            await SaveChangesInTransactionAsync();
        }

        public async Task UpdateCustomerInTransactionAsync(Customer customer)
        {
            _context.Customers.Update(customer);
            await SaveChangesInTransactionAsync();
        }

        public async Task UpdateStaffInTransactionAsync(Staff staff)
        {
            _context.Staffs.Update(staff);
            await SaveChangesInTransactionAsync();
        }

        public Task<PasswordResetToken?> GetPasswordResetTokenWithUsersAsync(string tokenHash)
        {
            return _context.PasswordResetTokens
                .Include(x => x.Customer)
                .Include(x => x.Staff)
                .SingleOrDefaultAsync(x => x.TokenHash == tokenHash);
        }

        public async Task RevokeOutstandingPasswordResetTokensAsync(
            long? customerId,
            long? staffId,
            DateTime utcNow,
            long? exceptTokenId = null)
        {
            IQueryable<PasswordResetToken> query = _context.PasswordResetTokens
                .Where(x => !x.UsedAt.HasValue && !x.RevokedAt.HasValue && x.ExpiresAt > utcNow);

            if (customerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == customerId.Value);
            }
            else if (staffId.HasValue)
            {
                query = query.Where(x => x.StaffId == staffId.Value);
            }

            if (exceptTokenId.HasValue)
            {
                query = query.Where(x => x.Id != exceptTokenId.Value);
            }

            var tokens = await query.ToListAsync();
            foreach (var token in tokens)
            {
                token.RevokedAt = utcNow;
            }
        }

        public async Task AddPasswordResetTokenInTransactionAsync(PasswordResetToken token)
        {
            _context.PasswordResetTokens.Add(token);
            await SaveChangesInTransactionAsync();
        }

        public async Task SavePasswordResetChangesInTransactionAsync(Customer? customer, Staff? staff, PasswordResetToken token)
        {
            if (customer != null)
            {
                _context.Customers.Update(customer);
            }

            if (staff != null)
            {
                _context.Staffs.Update(staff);
            }

            try
            {
                await SaveChangesInTransactionAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new UnauthorizedAccessException("Invalid or expired reset token.");
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
