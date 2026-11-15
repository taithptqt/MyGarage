using System;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Auth
{
    public interface IAuthPasswordResetStore
    {
        Task<PasswordResetToken?> GetPasswordResetTokenWithUsersAsync(string tokenHash);
        Task RevokeOutstandingPasswordResetTokensAsync(long? customerId, long? staffId, DateTime utcNow, long? exceptTokenId = null);
        Task AddPasswordResetTokenInTransactionAsync(PasswordResetToken token);
        Task SavePasswordResetChangesInTransactionAsync(Customer? customer, Staff? staff, PasswordResetToken token);
    }
}
