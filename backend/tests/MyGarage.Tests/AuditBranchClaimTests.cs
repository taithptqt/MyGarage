using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Security;
using Xunit;

namespace MyGarage.Tests;

public sealed class AuditBranchClaimTests
{
    [Fact]
    public async Task AuditWrite_UsesBranchIdClaim_WhenEntityHasNoBranch()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "200"),
            new Claim(ClaimTypes.Role, "Staff"),
            new Claim("branch_id", "42")
        }, "test")) };
        await using var db = new GarageContext(options, new HttpAuditContextAccessor(new HttpContextAccessor { HttpContext = http }));
        db.Customers.Add(new Customer { Id = 900, FullName = "Audit Branch", Phone = "0900000900", Type = "INDIVIDUAL" });
        await db.SaveChangesAsync();
        var audit = await db.AuditLogs.SingleAsync(x => x.TableName == "Customers" || x.TableName == "Customer");
        Assert.Equal(42, audit.BranchId);
        Assert.Equal(200, audit.UserId);
    }
}
