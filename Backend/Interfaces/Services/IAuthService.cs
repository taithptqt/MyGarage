using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;

namespace MyGarage.Interfaces.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
        Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginRequestDto request);
        Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
        
        Task ForgotPasswordAsync(ForgotPasswordRequestDto request);
        Task ResetPasswordAsync(ResetPasswordRequestDto request);
        Task ChangePasswordAsync(long userId, string role, ChangePasswordRequestDto request);
        Task<UserProfileResponseDto> GetProfileAsync(long userId, string role);
    }
}
