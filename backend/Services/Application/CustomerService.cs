using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class CustomerService : GenericService<Customer>, ICustomerService
    {
        private readonly IGarageContext _context;

        public CustomerService(ICustomerRepository repository, IGarageContext context) : base(repository)
        {
            _context = context;
        }

        public override async Task<Customer> AddAsync(Customer entity)
        {
            if (!string.IsNullOrEmpty(entity.PasswordHash))
            {
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(entity.PasswordHash);
            }

            return await base.AddAsync(entity);
        }

        public override async Task<Customer?> UpdateAsync(object id, Customer entity)
        {
            if (!string.IsNullOrEmpty(entity.PasswordHash) &&
                !entity.PasswordHash.StartsWith("$2a$") &&
                !entity.PasswordHash.StartsWith("$2b$") &&
                !entity.PasswordHash.StartsWith("$2y$"))
            {
                // Only hash if it looks like a raw password and not already a bcrypt hash.
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(entity.PasswordHash);
            }

            return await base.UpdateAsync(id, entity);
        }

        public override async Task<IEnumerable<Customer>> GetAllAsync()
        {
            return await _context.Customers
                .Where(customer => customer.IsDeleted != true)
                .ToListAsync();
        }

        public override async Task<Customer?> GetByIdAsync(object id)
        {
            if (!TryResolveCustomerId(id, out var customerId))
            {
                return null;
            }

            return await _context.Customers
                .FirstOrDefaultAsync(customer => customer.Id == customerId && customer.IsDeleted != true);
        }

        public override async Task<bool> DeleteAsync(object id)
        {
            if (!TryResolveCustomerId(id, out var customerId))
            {
                return false;
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(item => item.Id == customerId && item.IsDeleted != true);
            if (customer == null)
            {
                return false;
            }

            customer.IsDeleted = true;
            customer.IsActive = false;
            customer.DeletedAt = VietnamTime.Now;
            customer.UpdatedAt = VietnamTime.Now;
            customer.SecurityStamp = Guid.NewGuid().ToString("N");

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Customer>> GetByBranchIdAsync(long branchId)
        {
            var preferredBranchTag = $"[PreferredBranchId:{branchId}]";

            var customerIdsByBranch = _context.ServiceOrders
                .Where(so => so.OriginBranchId == branchId || so.ExecuteBranchId == branchId)
                .Select(so => so.CustomerId)
                .Distinct();

            var customerIdsByAppointment = _context.Appointments
                .Where(a => a.BranchId == branchId)
                .Select(a => a.CustomerId)
                .Distinct();

            var customerIds = await customerIdsByBranch
                .Union(customerIdsByAppointment)
                .ToListAsync();

            return await _context.Customers
                .Where(c =>
                    c.IsDeleted != true &&
                    (customerIds.Contains(c.Id) ||
                    (c.Note ?? string.Empty).Contains(preferredBranchTag)))
                .ToListAsync();
        }

        private static bool TryResolveCustomerId(object id, out long customerId)
        {
            switch (id)
            {
                case long value:
                    customerId = value;
                    return true;
                case int value:
                    customerId = value;
                    return true;
                case string text when long.TryParse(text, out var parsed):
                    customerId = parsed;
                    return true;
                default:
                    customerId = default;
                    return false;
            }
        }
    }
}
