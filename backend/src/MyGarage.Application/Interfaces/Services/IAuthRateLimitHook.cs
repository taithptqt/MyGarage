using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IAuthRateLimitHook
    {
        Task<bool> CanRequestPasswordResetAsync(string normalizedEmail);
        Task<bool> CanSubmitPasswordResetAsync(string tokenFingerprint);
    }
}
