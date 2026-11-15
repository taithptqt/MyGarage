using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;

namespace MyGarage.Interfaces
{
    public interface IAuthLoginAndTokenService
    {
        Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
        Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginRequestDto request);
        Task<AuthResponseDto> GoogleLoginValidatedAsync(GoogleTokenPayload payload, GoogleLoginRequestDto request);
    }
}
