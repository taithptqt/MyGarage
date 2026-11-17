using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class StaffBranchSessionTests
{
    [Fact]
    public async Task BranchTransfer_RevokesOldSession_AndNewSessionUsesNewBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.RetailOrders.AddRange(
                new RetailOrder { Id = 8101, BranchId = 1, CustomerId = 100, OrderCode = "BR1" },
                new RetailOrder { Id = 8102, BranchId = 2, CustomerId = 100, OrderCode = "BR2" });
            db.Invoices.AddRange(
                new Invoice { Id = 8201, RetailOrderId = 8101, PayerType = "CUSTOMER", TotalAmount = 10 },
                new Invoice { Id = 8202, RetailOrderId = 8102, PayerType = "CUSTOMER", TotalAmount = 20 });
            return Task.CompletedTask;
        });

        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        using var staff = factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/invoices/8201")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync("/api/v1/invoices/8202")).StatusCode);

        using var profileUpdate = await admin.PutAsJsonAsync("/api/v1/staff/200", new { fullName = "Updated Name" });
        Assert.Equal(HttpStatusCode.OK, profileUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/invoices/8201")).StatusCode);

        using var transfer = await admin.PutAsJsonAsync("/api/v1/staff/200", new { branchId = 2 });
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/v1/invoices/8201")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/v1/invoices/8202")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var updated = await db.Staffs.AsNoTracking().SingleAsync(x => x.Id == 200);
        Assert.Equal(2, updated.BranchId);
        Assert.NotEqual("test-stamp", updated.SecurityStamp);

        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 2, updated.SecurityStamp!));
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync("/api/v1/invoices/8201")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/invoices/8202")).StatusCode);
    }
}
