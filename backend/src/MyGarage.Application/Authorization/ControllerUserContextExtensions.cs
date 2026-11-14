using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Authorization;

namespace MyGarage.Controllers
{
    internal static class ControllerUserContextExtensions
    {
        internal static long? GetCurrentUserId(this ControllerBase controller) =>
            controller.User.GetCurrentUserId();

        internal static long? GetCurrentUserId(this ClaimsPrincipal? user)
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

        internal static string? GetCurrentRole(this ControllerBase controller) =>
            controller.User.GetCurrentRole();

        internal static string? GetCurrentRole(this ClaimsPrincipal? user)
        {
            var claim = user?.FindFirst(ClaimTypes.Role)
                ?? user?.FindFirst("role")
                ?? user?.FindFirst("Role");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim!.Value.Trim();
        }

        internal static string? GetCurrentRoleCode(this ControllerBase controller) =>
            controller.User.GetCurrentRoleCode();

        internal static string? GetCurrentRoleCode(this ClaimsPrincipal? user)
        {
            var claim = user?.FindFirst("role_code");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim!.Value.Trim();
        }

        internal static string? GetCurrentRoleName(this ControllerBase controller) =>
            controller.User.GetCurrentRoleName();

        internal static string? GetCurrentRoleName(this ClaimsPrincipal? user)
        {
            var claim = user?.FindFirst("role_name");
            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim!.Value.Trim();
        }

        internal static long? GetCurrentBranchId(this ControllerBase controller) =>
            controller.User.GetCurrentBranchId();

        internal static long? GetCurrentBranchId(this ClaimsPrincipal? user)
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

        internal static bool IsStaffUser(this ControllerBase controller) =>
            controller.User.IsStaffUser();

        internal static bool IsStaffUser(this ClaimsPrincipal? user)
        {
            return user.GetCurrentUserId().HasValue
                && string.Equals(user.GetCurrentRole(), "Staff", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsAdminStaff(this ControllerBase controller) =>
            controller.User.IsAdminStaff();

        internal static bool IsAdminStaff(this ClaimsPrincipal? user)
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

        internal static bool IsStaffWithAnyKeyword(this ControllerBase controller, params string[] keywords) =>
            controller.User.IsStaffWithAnyKeyword(keywords);

        internal static bool IsStaffWithAnyKeyword(this ClaimsPrincipal? user, params string[] keywords)
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

        internal static bool TryResolveBranchScope(
            this ControllerBase controller,
            long? requestedBranchId,
            out long? scopedBranchId,
            out IActionResult? deniedResult)
        {
            scopedBranchId = null;
            deniedResult = null;

            if (!controller.IsStaffUser())
            {
                deniedResult = controller.Forbid();
                return false;
            }

            if (controller.IsAdminStaff())
            {
                scopedBranchId = requestedBranchId.HasValue && requestedBranchId.Value > 0
                    ? requestedBranchId.Value
                    : null;
                return true;
            }

            var currentBranchId = controller.GetCurrentBranchId();
            if (!currentBranchId.HasValue)
            {
                deniedResult = controller.Forbid();
                return false;
            }

            if (requestedBranchId.HasValue && requestedBranchId.Value > 0 && requestedBranchId.Value != currentBranchId.Value)
            {
                deniedResult = controller.Forbid();
                return false;
            }

            scopedBranchId = currentBranchId.Value;
            return true;
        }

        internal static bool TryResolveRequiredBranchId(
            this ControllerBase controller,
            long? requestedBranchId,
            out long branchId,
            out IActionResult? deniedResult)
        {
            branchId = 0;
            if (!controller.TryResolveBranchScope(requestedBranchId, out var scopedBranchId, out deniedResult))
            {
                return false;
            }

            var resolved = scopedBranchId ?? controller.GetCurrentBranchId();
            if (!resolved.HasValue || resolved.Value <= 0)
            {
                deniedResult = controller.BadRequest(new { message = "BranchId is required." });
                return false;
            }

            branchId = resolved.Value;
            return true;
        }

        internal static UserActorContext ToUserActorContext(this ControllerBase controller)
        {
            return new UserActorContext(
                controller.GetCurrentUserId(),
                controller.GetCurrentRole(),
                controller.GetCurrentRoleCode(),
                controller.GetCurrentBranchId(),
                controller.IsAdminStaff()
            );
        }
    }
}
