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

            if (!string.Equals(role, "Customer", StringComparison.Ordinal) &&
                !string.Equals(role, "Staff", StringComparison.Ordinal))
            {
                return false;
            }

            if (role == "Customer")
            {
                var customer = await _context.Customers.AsNoTracking()
                    .Where(x => x.Id == userId && x.IsDeleted != true && x.IsActive != false)
                    .Select(x => new { x.SecurityStamp }).FirstOrDefaultAsync(cancellationToken);
                return customer != null && IsStampValid(customer.SecurityStamp, stampClaim);
            }
            var staff = await _context.Staffs.AsNoTracking()
                .Where(x => x.Id == userId && x.IsDeleted != true)
                .Select(x => new { x.SecurityStamp, x.Status }).FirstOrDefaultAsync(cancellationToken);
            return staff != null && string.Equals(staff.Status?.Trim(), "ACTIVE", StringComparison.OrdinalIgnoreCase)
                && IsStampValid(staff.SecurityStamp, stampClaim);
        }

        public void Invalidate(string role, long userId)
        {
            // Validation reads the current database value on every request.
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
