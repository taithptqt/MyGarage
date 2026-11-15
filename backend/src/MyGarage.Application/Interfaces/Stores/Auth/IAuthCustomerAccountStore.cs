using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Auth
{
    public interface IAuthCustomerAccountStore
    {
        Task<Customer?> GetCustomerByIdAsync(long customerId);
        Task<Customer?> GetCustomerByEmailAsync(string normalizedEmail);
        Task<Customer?> GetCustomerByPhoneAsync(string normalizedPhone);
        Task<Customer?> GetCustomerByGoogleIdAsync(string googleId);

        Task<bool> IsCustomerPhoneTakenAsync(string phone, long? excludingCustomerId = null);

        Task AddCustomerInTransactionAsync(Customer customer);
        Task UpdateCustomerInTransactionAsync(Customer customer);
    }
}
