using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Services.Application.Chat
{
    public sealed class ChatCallerCustomerResolver : IChatCallerCustomerResolver
    {
        private readonly IGarageContext _context;

        public ChatCallerCustomerResolver(IGarageContext context)
        {
            _context = context;
        }

        public async Task<long> ResolveCustomerIdAsync(long callerUserId, string? role, CancellationToken ct = default)
        {
            var isCustomerRole = string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase);

            if (isCustomerRole)
            {
                var customerExists = await _context.Customers
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == callerUserId && (c.IsDeleted != true || c.IsDeleted == null), ct);

                if (customerExists)
                {
                    return callerUserId;
                }
            }

            // If caller is Staff or Admin, callerUserId represents Staff.Id, NOT Customer.Id.
            // In SQL Server, Conversations.CustomerId has FK constraint to dbo.Customers(Id).
            // We resolve or create a linked Customer record for the staff user to satisfy FK.
            var staff = await _context.Staffs
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == callerUserId && (s.IsDeleted != true || s.IsDeleted == null), ct);

            if (staff != null)
            {
                if (!string.IsNullOrWhiteSpace(staff.Email))
                {
                    var existingByEmail = await _context.Customers
                        .FirstOrDefaultAsync(c => c.Email == staff.Email && (c.IsDeleted != true || c.IsDeleted == null), ct);
                    if (existingByEmail != null)
                    {
                        return existingByEmail.Id;
                    }
                }

                if (!string.IsNullOrWhiteSpace(staff.Phone))
                {
                    var existingByPhone = await _context.Customers
                        .FirstOrDefaultAsync(c => c.Phone == staff.Phone && (c.IsDeleted != true || c.IsDeleted == null), ct);
                    if (existingByPhone != null)
                    {
                        return existingByPhone.Id;
                    }
                }

                var linkedCustomer = new Customer
                {
                    FullName = !string.IsNullOrWhiteSpace(staff.FullName) ? staff.FullName : "MyGarage Staff",
                    Email = staff.Email,
                    Phone = staff.Phone,
                    IsActive = true,
                    Note = $"Tài khoản khách hàng liên kết nhân viên #{staff.Id}"
                };

                _context.Customers.Add(linkedCustomer);
                await _context.SaveChangesAsync(ct);
                return linkedCustomer.Id;
            }

            // Fallback: check if callerUserId is already in Customers
            var fallbackCustomer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == callerUserId && (c.IsDeleted != true || c.IsDeleted == null), ct);

            if (fallbackCustomer != null)
            {
                return fallbackCustomer.Id;
            }

            // Last-resort fallback: create active customer record
            var guestCustomer = new Customer
            {
                FullName = "Khách hàng MyGarage",
                IsActive = true,
                Note = $"Tài khoản khách hàng liên kết với user #{callerUserId}"
            };

            _context.Customers.Add(guestCustomer);
            await _context.SaveChangesAsync(ct);
            return guestCustomer.Id;
        }
    }
}
