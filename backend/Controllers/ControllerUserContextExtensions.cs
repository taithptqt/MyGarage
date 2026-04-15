using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace MyGarage.Controllers
{
    internal static class ControllerUserContextExtensions
    {
        internal static long? GetCurrentUserId(this ControllerBase controller)
        {
            var claim = controller.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? controller.User.FindFirst("sub")
                ?? controller.User.FindFirst("id")
                ?? controller.User.FindFirst("uid");
            return claim != null && long.TryParse(claim.Value, out var id)
                ? id
                : null;
        }

        internal static string? GetCurrentRole(this ControllerBase controller)
        {
            var claim = controller.User.FindFirst(ClaimTypes.Role)
                ?? controller.User.FindFirst("role")
                ?? controller.User.FindFirst("Role");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim!.Value.Trim();
        }

        internal static string? GetCurrentRoleCode(this ControllerBase controller)
        {
            var claim = controller.User.FindFirst("role_code");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim!.Value.Trim();
        }

        internal static string? GetCurrentRoleName(this ControllerBase controller)
        {
            var claim = controller.User.FindFirst("role_name");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim!.Value.Trim();
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

        internal static bool IsStaffInAnyRole(this ControllerBase controller, params string[] roleCodes)
        {
            var claimRole = controller.GetCurrentRole();
            if (!string.Equals(claimRole, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var currentRoleCode = controller.GetCurrentRoleCode();
            if (string.IsNullOrWhiteSpace(currentRoleCode))
            {
                return false;
            }

            return roleCodes.Any(code =>
                !string.IsNullOrWhiteSpace(code) &&
                string.Equals(currentRoleCode, code.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsServiceAdvisorOrReceptionist(this ControllerBase controller)
        {
            if (!string.Equals(controller.GetCurrentRole(), "Staff", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var roleCode = (controller.GetCurrentRoleCode() ?? string.Empty).Trim().ToUpperInvariant();
            var roleName = (controller.GetCurrentRoleName() ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(roleCode) && string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            var advisorRoleCodes = new[]
            {
                "SERVICE_ADVISOR",
                "ADVISOR",
                "CVDV",
                "RECEPTIONIST",
                "FRONT_DESK"
            };

            if (advisorRoleCodes.Any(code => roleCode.Contains(code, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return roleName.Contains("CO VAN", StringComparison.OrdinalIgnoreCase)
                || roleName.Contains("DICH VU", StringComparison.OrdinalIgnoreCase)
                || roleName.Contains("LE TAN", StringComparison.OrdinalIgnoreCase)
                || roleName.Contains("TIEP TAN", StringComparison.OrdinalIgnoreCase);
        }
    }
}
