using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Auth;

namespace MyGarage.Interfaces
{
    public interface IAuthPasswordResetService
    {
        Task ChangePasswordAsync(long userId, string role, ChangePasswordRequestDto request);
        Task ForgotPasswordAsync(ForgotPasswordRequestDto request);
        Task ResetPasswordAsync(ResetPasswordRequestDto request);
    }
}
