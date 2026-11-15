using System;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAuthPasswordResetDeliveryService
    {
        string ComputeTokenHash(string token);
        Task IssueAndSendResetTokenAsync(long? customerId, long? staffId, string normalizedEmail);
        Task<(PasswordResetToken Token, DateTime UtcNow)> GetValidResetTokenAsync(string tokenHash);
        Task ConsumeResetTokenAsync(PasswordResetToken token, DateTime utcNow);
    }
}
