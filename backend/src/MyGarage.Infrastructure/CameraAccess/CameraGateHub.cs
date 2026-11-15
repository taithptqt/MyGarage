using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MyGarage.Domain.Rules;

namespace MyGarage.Infrastructure.CameraAccess;

[Authorize(Roles = "Staff")]
public class CameraGateHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirst("role_code")?.Value ?? "";
        if (!CameraAccessRules.CanRead(role)) { Context.Abort(); return; }
        if (role == "ADMIN") await Groups.AddToGroupAsync(Context.ConnectionId, "gate-admin");
        else if (long.TryParse(Context.User?.FindFirst("branch_id")?.Value, out var branch) && branch > 0)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"gate-{branch}");
        else { Context.Abort(); return; }
        await base.OnConnectedAsync();
    }
}
