using MyGarage.Models;
using MyGarage.Interfaces;
using System.Linq;

namespace MyGarage.Services
{
    public class CustomerService : GenericService<Customer>, ICustomerService
    {
        public CustomerService(ICustomerRepository repository) : base(repository)
        {
        }

        public override async System.Threading.Tasks.Task<Customer> AddAsync(Customer entity)
        {
            NormalizeAndValidatePhone(entity);
            if (!string.IsNullOrEmpty(entity.PasswordHash))
            {
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(entity.PasswordHash);
            }
            return await base.AddAsync(entity);
        }

        public override async System.Threading.Tasks.Task<Customer?> UpdateAsync(object id, Customer entity)
        {
            NormalizeAndValidatePhone(entity);
            if (!string.IsNullOrEmpty(entity.PasswordHash) && !entity.PasswordHash.StartsWith("$2a$") && !entity.PasswordHash.StartsWith("$2b$") && !entity.PasswordHash.StartsWith("$2y$"))
            {
                // Only hash if it looks like a raw password and not already a bcrypt hash
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(entity.PasswordHash);
            }
            return await base.UpdateAsync(id, entity);
        }

        private static void NormalizeAndValidatePhone(Customer entity)
        {
            var normalized = NormalizePhone(entity.Phone);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("Phone is required.");
            }
            if (!IsValidVietnamPhone(normalized))
            {
                throw new InvalidOperationException("Phone format is invalid.");
            }
            entity.Phone = normalized;
        }

        private static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            var digits = new string(phone.Trim().Where(char.IsDigit).ToArray());
            if (digits.StartsWith("84", System.StringComparison.Ordinal) && (digits.Length == 11 || digits.Length == 12))
            {
                digits = "0" + digits.Substring(2);
            }
            return digits;
        }

        private static bool IsValidVietnamPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            if (!phone.All(char.IsDigit)) return false;
            if (!phone.StartsWith("0", System.StringComparison.Ordinal)) return false;
            return phone.Length == 10 || phone.Length == 11;
        }
    }
}
