using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Security;
using Xunit;

namespace MyGarage.Tests;

public sealed class AuditContextConsistencyTests
{
    private sealed class StubHttpContextAccessor(HttpContext? httpContext) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }

    [Fact]
    public async Task SaveChangesAsync_UsesInjectedHttpContextAccessor_ForAuditLogs()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-audit-42"
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        httpContext.Request.Headers.UserAgent = "AuditTestAgent/1.0";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("branchId", "7")
        ], "TestAuth"));

        var accessor = new StubHttpContextAccessor(httpContext);
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase("AuditContextConsistency_" + Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new GarageContext(options, new HttpAuditContextAccessor(accessor));
        db.Categories.Add(new Category { CategoryName = "Dầu nhớt" });

        var saved = await db.SaveChangesAsync();
        Assert.Equal(1, saved);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(42, audit.UserId);
        Assert.Equal("STAFF", audit.UserType);
        Assert.Equal(7, audit.BranchId);
        Assert.Equal("CREATE", audit.Action);
        Assert.Equal("127.0.0.1", audit.IpAddress);
        Assert.Equal("AuditTestAgent/1.0", audit.UserAgent);
        Assert.Equal("trace-audit-42", audit.SessionId);
    }

    [Fact]
    public async Task SaveChangesAsync_WithCustomerClaim_NormalizesUserTypeToCustomer()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "99"),
            new Claim(ClaimTypes.Role, "Customer")
        ], "TestAuth"));

        var accessor = new StubHttpContextAccessor(httpContext);
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase("AuditContextCustomer_" + Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new GarageContext(options, new HttpAuditContextAccessor(accessor));
        db.Categories.Add(new Category { CategoryName = "Lọc gió" });

        await db.SaveChangesAsync();

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(99, audit.UserId);
        Assert.Equal("CUSTOMER", audit.UserType);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenHttpContextIsNull_FallsBackSafely()
    {
        var accessor = new StubHttpContextAccessor(null);
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase("AuditContextNull_" + Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new GarageContext(options, new HttpAuditContextAccessor(accessor));
        db.Categories.Add(new Category { CategoryName = "Phụ tùng" });

        await db.SaveChangesAsync();

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(0, audit.UserId);
        Assert.Equal("STAFF", audit.UserType);
    }
}
