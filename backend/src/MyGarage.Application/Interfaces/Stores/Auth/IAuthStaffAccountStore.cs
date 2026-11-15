using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Auth
{
    public interface IAuthStaffAccountStore
    {
        Task<Staff?> GetStaffByIdAsync(long staffId);
        Task<Staff?> GetStaffByEmailAsync(string normalizedEmail);
        Task<Staff?> GetStaffByPhoneAsync(string normalizedPhone);
        Task<Staff?> GetStaffByGoogleIdAsync(string googleId);

        Task<bool> EmailExistsAsync(string normalizedEmail);
        Task<bool> IsStaffPhoneTakenAsync(string phone, long? excludingStaffId = null);

        Task UpdateStaffInTransactionAsync(Staff staff);
    }
}
