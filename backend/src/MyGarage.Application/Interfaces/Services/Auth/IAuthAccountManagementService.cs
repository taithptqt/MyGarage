using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;

namespace MyGarage.Interfaces
{
    public interface IAuthAccountManagementService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
        Task<UserProfileResponseDto> GetProfileAsync(long userId, string role);
    }
}
