using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AuthSecurityStampValidator : IAuthSecurityStampValidator
    {
        private readonly IGarageContext _context;

        public AuthSecurityStampValidator(IGarageContext context)
        {
            _context = context;
        }

        public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = principal.FindFirst(ClaimTypes.Role)?.Value;
            var stampClaim = principal.FindFirst("security_stamp")?.Value;

            if (!long.TryParse(userIdClaim, out var userId) || string.IsNullOrWhiteSpace(role))
            {
                return false;
            }

            if (role == "Customer")
            {
                var currentStamp = await _context.Customers
                    .Where(x => x.Id == userId)
                    .Select(x => x.SecurityStamp)
                    .FirstOrDefaultAsync(cancellationToken);

                return IsStampValid(currentStamp, stampClaim);
            }

            if (role == "Staff")
            {
                var currentStamp = await _context.Staffs
                    .Where(x => x.Id == userId)
                    .Select(x => x.SecurityStamp)
                    .FirstOrDefaultAsync(cancellationToken);

                return IsStampValid(currentStamp, stampClaim);
            }

            return false;
        }

        private static bool IsStampValid(string? currentStamp, string? stampClaim)
        {
            if (string.IsNullOrWhiteSpace(currentStamp))
            {
                return string.IsNullOrWhiteSpace(stampClaim);
            }

            return string.Equals(currentStamp, stampClaim, StringComparison.Ordinal);
        }
    }
}
