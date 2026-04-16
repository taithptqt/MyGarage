using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace MyGarage.Controllers
{
    internal static class ControllerUserContextExtensions
    {
        internal static long? GetCurrentUserId(this ControllerBase controller)
        {
            var claim = controller.User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && long.TryParse(claim.Value, out var id)
                ? id
                : null;
        }

        internal static string? GetCurrentRole(this ControllerBase controller)
        {
            var claim = controller.User.FindFirst(ClaimTypes.Role)
                ?? controller.User.FindFirst("role")
                ?? controller.User.FindFirst("Role");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim.Value.Trim();
        }

        internal static string? GetCurrentRoleCode(this ControllerBase controller)
        {
            var claim = controller.User.FindFirst("role_code");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim.Value.Trim();
        }

        internal static string? GetCurrentRoleName(this ControllerBase controller)
        {
            var claim = controller.User.FindFirst("role_name");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim.Value.Trim();
        }

        internal static long? GetCurrentBranchId(this ControllerBase controller)
        {
            var claim = controller.User.FindFirst("branch_id")
                ?? controller.User.FindFirst("branchId")
                ?? controller.User.FindFirst("BranchId");
            return claim != null && long.TryParse(claim.Value, out var branchId) && branchId > 0
                ? branchId
                : null;
        }

        internal static bool IsCurrentAccountingStaff(this ControllerBase controller)
        {
            var claimRole = controller.GetCurrentRole();
            var roleCode = controller.GetCurrentRoleCode();
            var roleNameUpper = NormalizeUpperInvariant(controller.GetCurrentRoleName());

            return string.Equals(claimRole, "Staff", StringComparison.OrdinalIgnoreCase)
                   && (
                       string.Equals(roleCode, "ACCOUNTANT", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(roleCode, "ACCOUNTING", StringComparison.OrdinalIgnoreCase)
                       || roleNameUpper.Contains("ACCOUNTANT", StringComparison.OrdinalIgnoreCase)
                       || roleNameUpper.Contains("KE TOAN", StringComparison.OrdinalIgnoreCase)
                   );
        }

        internal static bool IsCurrentAdminStaff(this ControllerBase controller)
        {
            var claimRole = controller.GetCurrentRole();
            var roleCode = controller.GetCurrentRoleCode();
            var roleNameUpper = NormalizeUpperInvariant(controller.GetCurrentRoleName());

            return string.Equals(claimRole, "Staff", StringComparison.OrdinalIgnoreCase)
                   && (
                       string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase)
                       || roleNameUpper.Contains("ADMIN", StringComparison.OrdinalIgnoreCase)
                   );
        }

        internal static bool IsCurrentFinancePrivilegedStaff(this ControllerBase controller)
        {
            return controller.IsCurrentAccountingStaff() || controller.IsCurrentAdminStaff();
        }

        internal static bool IsCurrentStaff(this ControllerBase controller)
        {
            var claimRole = controller.GetCurrentRole();
            return string.Equals(claimRole, "Staff", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool RequiresFinanceBranchScope(this ControllerBase controller)
        {
            return controller.IsCurrentStaff() && !controller.IsCurrentAdminStaff();
        }

        private static string NormalizeUpperInvariant(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant();
        }
    }
}
