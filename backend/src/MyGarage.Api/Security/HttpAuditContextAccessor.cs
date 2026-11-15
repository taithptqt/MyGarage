using MyGarage.Application.Interfaces.Common;
using MyGarage.Controllers;

namespace MyGarage.Security;

public sealed class HttpAuditContextAccessor : IAuditContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpAuditContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public AuditActorSnapshot? GetCurrentContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var user = httpContext.User;
        return new AuditActorSnapshot(
            UserId: user.GetCurrentUserId(),
            Role: user.GetCurrentRole(),
            BranchId: user.GetCurrentBranchId(),
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
            TraceId: httpContext.TraceIdentifier);
    }
}
