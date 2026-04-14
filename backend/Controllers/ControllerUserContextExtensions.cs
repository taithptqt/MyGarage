using System.Security.Claims;
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
    }
}
