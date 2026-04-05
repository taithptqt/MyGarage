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
            return controller.User.FindFirst(ClaimTypes.Role)?.Value;
        }

        internal static string? GetCurrentRoleCode(this ControllerBase controller)
        {
            return controller.User.FindFirst("role_code")?.Value;
        }

        internal static string? GetCurrentRoleName(this ControllerBase controller)
        {
            return controller.User.FindFirst("role_name")?.Value;
        }

        internal static long? GetCurrentBranchId(this ControllerBase controller)
        {
            var claim = controller.User.FindFirst("branch_id")?.Value;
            return long.TryParse(claim, out var branchId) ? branchId : null;
        }
    }
}
