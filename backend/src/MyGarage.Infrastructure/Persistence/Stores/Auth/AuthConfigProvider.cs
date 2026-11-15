using System;
using Microsoft.Extensions.Configuration;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.Auth
{
    public sealed class AuthConfigProvider : IAuthConfigProvider
    {
        private readonly IConfiguration _configuration;

        public AuthConfigProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public int GetPasswordResetTokenExpiryMinutes()
        {
            var configured = _configuration["Auth:PasswordResetTokenExpiryMinutes"];
            return int.TryParse(configured, out var minutes) && minutes > 0
                ? minutes
                : 15;
        }

        public string GetPasswordResetUrlBase()
        {
            var baseUrl = (_configuration["Auth:PasswordResetUrlBase"] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("Password reset URL is not configured.");
            }

            return baseUrl;
        }
    }
}
