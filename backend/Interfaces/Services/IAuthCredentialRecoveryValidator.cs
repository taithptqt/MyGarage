using MyGarage.DTOs.Requests.Auth;

namespace MyGarage.Interfaces
{
    public interface IAuthCredentialRecoveryValidator
    {
        void ValidateForgotPassword(ForgotPasswordRequestDto request);
        void ValidateResetPassword(ResetPasswordRequestDto request);
    }
}
