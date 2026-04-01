using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CustomerService : GenericService<Customer>, ICustomerService
    {
        private readonly GarageContext _context;

        public CustomerService(ICustomerRepository repository, GarageContext context) : base(repository)
        {
            _context = context;
        }

        public override async System.Threading.Tasks.Task<Customer> AddAsync(Customer entity)
        {
            if (!string.IsNullOrEmpty(entity.PasswordHash))
            {
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(entity.PasswordHash);
            }
            return await base.AddAsync(entity);
        }

        public override async System.Threading.Tasks.Task<Customer?> UpdateAsync(object id, Customer entity)
        {
            if (!string.IsNullOrEmpty(entity.PasswordHash) && !entity.PasswordHash.StartsWith("$2a$") && !entity.PasswordHash.StartsWith("$2b$") && !entity.PasswordHash.StartsWith("$2y$"))
            {
                // Only hash if it looks like a raw password and not already a bcrypt hash
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(entity.PasswordHash);
            }
            return await base.UpdateAsync(id, entity);
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
                    customerIds.Contains(c.Id) ||
                    (c.Note ?? string.Empty).Contains(preferredBranchTag))
                .ToListAsync();
        }
    }
}
