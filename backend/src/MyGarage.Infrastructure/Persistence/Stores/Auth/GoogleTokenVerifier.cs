using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.Auth
{
    public sealed class GoogleTokenVerifier : IGoogleTokenVerifier
    {
        private readonly IConfiguration _configuration;

        public GoogleTokenVerifier(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<GoogleTokenPayload> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new List<string> { _configuration["GoogleAuth:ClientId"] ?? throw new InvalidOperationException("Google ClientId missing") }
            };

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("Invalid Google Token.", ex);
            }

            return new GoogleTokenPayload(
                payload.Email,
                payload.EmailVerified,
                payload.Subject,
                payload.Name);
        }
    }
}
