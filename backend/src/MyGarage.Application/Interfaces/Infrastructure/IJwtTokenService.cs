using MyGarage.DTOs.Responses.Auth;

namespace MyGarage.Interfaces
{
    public interface IJwtTokenService
    {
        AuthResponseDto GenerateToken(
            string userId,
            string name,
            string email,
            string phone,
            string avatarUrl,
            string role,
            string roleCode,
            string securityStamp,
            string roleName,
            long? branchId = null,
            string branchName = "");
    }
}
