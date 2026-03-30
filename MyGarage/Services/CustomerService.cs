using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CustomerService : GenericService<Customer>, ICustomerService
    {
        public CustomerService(ICustomerRepository repository) : base(repository)
        {
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
    }
}
