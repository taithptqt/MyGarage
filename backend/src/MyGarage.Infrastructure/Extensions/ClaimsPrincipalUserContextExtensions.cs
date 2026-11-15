using System;
using System.Linq;
using System.Security.Claims;

namespace MyGarage.Controllers
{
    public static class ClaimsPrincipalUserContextExtensions
    {
        public static long? GetCurrentUserId(this ClaimsPrincipal? user)
        {
            if (user == null)
            {
                return null;
            }

            var claim = user.FindFirst(ClaimTypes.NameIdentifier)
                ?? user.FindFirst("sub")
                ?? user.FindFirst("id")
                ?? user.FindFirst("uid");
            return claim != null && long.TryParse(claim.Value, out var id)
                ? id
                : null;
        }

        public static string? GetCurrentRole(this ClaimsPrincipal? user)
        {
            var claim = user?.FindFirst(ClaimTypes.Role)
                ?? user?.FindFirst("role")
                ?? user?.FindFirst("Role");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim!.Value.Trim();
        }

        public static string? GetCurrentRoleCode(this ClaimsPrincipal? user)
        {
            var claim = user?.FindFirst("role_code");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim!.Value.Trim();
        }

        public static string? GetCurrentRoleName(this ClaimsPrincipal? user)
        {
            var claim = user?.FindFirst("role_name");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim!.Value.Trim();
        }

        public static long? GetCurrentBranchId(this ClaimsPrincipal? user)
        {
            if (user == null)
            {
                return null;
            }

            var claim = user.FindFirst("branch_id")
                ?? user.FindFirst("branchId")
                ?? user.FindFirst("BranchId");
            return claim != null && long.TryParse(claim.Value, out var branchId) && branchId > 0
                ? branchId
                : null;
        }

        public static bool IsStaffUser(this ClaimsPrincipal? user)
        {
            return user.GetCurrentUserId().HasValue
                && string.Equals(user.GetCurrentRole(), "Staff", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAdminStaff(this ClaimsPrincipal? user)
        {
            if (!user.IsStaffUser())
            {
                return false;
            }

            var roleCode = (user.GetCurrentRoleCode() ?? string.Empty).Trim();
            return string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleCode, "SUPERADMIN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleCode, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsStaffWithAnyKeyword(this ClaimsPrincipal? user, params string[] keywords)
        {
            if (!user.IsStaffUser())
            {
                return false;
            }

            var roleCode = (user.GetCurrentRoleCode() ?? string.Empty).Trim().ToUpperInvariant();
            var roleName = (user.GetCurrentRoleName() ?? string.Empty).Trim().ToUpperInvariant();

            return keywords.Any(keyword =>
                !string.IsNullOrWhiteSpace(keyword) &&
                (roleCode.Contains(keyword.Trim().ToUpperInvariant(), StringComparison.Ordinal) ||
                 roleName.Contains(keyword.Trim().ToUpperInvariant(), StringComparison.Ordinal)));
        }
    }
}
