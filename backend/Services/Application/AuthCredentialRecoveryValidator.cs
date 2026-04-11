using System;
using System.ComponentModel.DataAnnotations;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AuthCredentialRecoveryValidator : IAuthCredentialRecoveryValidator
    {
        private static readonly EmailAddressAttribute EmailValidator = new();

        public void ValidateForgotPassword(ForgotPasswordRequestDto request)
        {
            if (request == null)
            {
                throw new ValidationException("Request is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Email) || !EmailValidator.IsValid(request.Email))
            {
                throw new ValidationException("A valid email address is required.");
            }
        }

        public void ValidateResetPassword(ResetPasswordRequestDto request)
        {
            if (request == null)
            {
                throw new ValidationException("Request is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Token))
            {
                throw new ValidationException("Reset token is required.");
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            {
                throw new ValidationException("New password must be at least 6 characters.");
            }
        }
    }
}
