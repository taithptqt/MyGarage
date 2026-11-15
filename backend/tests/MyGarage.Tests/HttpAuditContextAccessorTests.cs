using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MyGarage.Security;
using Xunit;

namespace MyGarage.Tests;

public sealed class HttpAuditContextAccessorTests
{
    [Fact]
    public void GetCurrentContext_WhenHttpContextIsNull_ReturnsNull()
    {
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
        var sut = new HttpAuditContextAccessor(httpContextAccessor);

        var snapshot = sut.GetCurrentContext();

        Assert.Null(snapshot);
    }

    [Fact]
    public void GetCurrentContext_WhenAuthenticatedRequestPresent_MapsClaimsAndRequestMetadata()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-123"
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        httpContext.Request.Headers.UserAgent = "MyGarageTestAgent/1.0";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Role, "Manager"),
            new Claim("branchId", "7")
        ], "TestAuth"));

        var sut = new HttpAuditContextAccessor(new HttpContextAccessor { HttpContext = httpContext });

        var snapshot = sut.GetCurrentContext();

        Assert.NotNull(snapshot);
        Assert.Equal(42L, snapshot.UserId);
        Assert.Equal("Manager", snapshot.Role);
        Assert.Equal(7L, snapshot.BranchId);
        Assert.Equal("127.0.0.1", snapshot.IpAddress);
        Assert.Equal("MyGarageTestAgent/1.0", snapshot.UserAgent);
        Assert.Equal("trace-123", snapshot.TraceId);
    }

    [Fact]
    public void GetCurrentContext_WhenAnonymousRequestPresent_LeavesActorClaimsNull()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-anon"
        };
        httpContext.Request.Headers.UserAgent = "AnonAgent/2.0";

        var sut = new HttpAuditContextAccessor(new HttpContextAccessor { HttpContext = httpContext });

        var snapshot = sut.GetCurrentContext();

        Assert.NotNull(snapshot);
        Assert.Null(snapshot.UserId);
        Assert.Null(snapshot.Role);
        Assert.Null(snapshot.BranchId);
        Assert.Null(snapshot.IpAddress);
        Assert.Equal("AnonAgent/2.0", snapshot.UserAgent);
        Assert.Equal("trace-anon", snapshot.TraceId);
    }
}
