using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AuthService : Interfaces.IAuthService
    {
        private readonly IAuthLoginAndTokenService _loginAndTokenService;
        private readonly IAuthAccountManagementService _accountManagementService;
        private readonly IAuthPasswordResetService _passwordResetService;

        public AuthService(
            IAuthLoginAndTokenService loginAndTokenService,
            IAuthAccountManagementService accountManagementService,
            IAuthPasswordResetService passwordResetService)
        {
            _loginAndTokenService = loginAndTokenService;
            _accountManagementService = accountManagementService;
            _passwordResetService = passwordResetService;
        }

        public Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            return _loginAndTokenService.LoginAsync(request);
        }

        public Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginRequestDto request)
        {
            return _loginAndTokenService.GoogleLoginAsync(request);
        }

        internal Task<AuthResponseDto> GoogleLoginValidatedAsync(GoogleTokenPayload payload, GoogleLoginRequestDto request)
        {
            return _loginAndTokenService.GoogleLoginValidatedAsync(payload, request);
        }

        internal static void EnsureGoogleIdentityMatches(string? existingGoogleId, string incomingGoogleId)
        {
            AuthLoginAndTokenService.EnsureGoogleIdentityMatches(existingGoogleId, incomingGoogleId);
        }

        public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            return _accountManagementService.RegisterAsync(request);
        }

        public Task ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            return _passwordResetService.ForgotPasswordAsync(request);
        }

        public Task ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            return _passwordResetService.ResetPasswordAsync(request);
        }

        public Task ChangePasswordAsync(long userId, string role, ChangePasswordRequestDto request)
        {
            return _passwordResetService.ChangePasswordAsync(userId, role, request);
        }

        public Task<UserProfileResponseDto> GetProfileAsync(long userId, string role)
        {
            return _accountManagementService.GetProfileAsync(userId, role);
        }

        public static string ComputePasswordResetTokenHashForTests(string token)
        {
            return AuthPasswordResetService.ComputePasswordResetTokenHashForTests(token);
        }
    }
}
