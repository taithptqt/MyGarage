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
    }
}
