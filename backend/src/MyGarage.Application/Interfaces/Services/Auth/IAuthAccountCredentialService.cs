using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public readonly record struct PasswordRecoveryTarget(long? CustomerId, long? StaffId);

    public interface IAuthAccountCredentialService
    {
        Task ChangePasswordAsync(long userId, string role, string oldPassword, string newPassword);
        Task<PasswordRecoveryTarget?> ResolveRecoveryTargetAsync(string normalizedEmail);
        (string Role, long UserId) PreparePasswordReset(PasswordResetToken token, string newPassword);
        void InvalidateUserSessions(string role, long userId);
    }
}
