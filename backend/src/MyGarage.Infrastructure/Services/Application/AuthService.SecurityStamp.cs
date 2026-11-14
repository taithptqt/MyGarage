using System;

namespace MyGarage.Services
{
    public partial class AuthService
    {
        private static string CreateSecurityStamp()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string GetOrCreateSecurityStamp(string? currentSecurityStamp)
        {
            return string.IsNullOrWhiteSpace(currentSecurityStamp)
                ? CreateSecurityStamp()
                : currentSecurityStamp;
        }
    }
}
