using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class CustomerLegacyScopeTests
{
    [Fact]
    public async Task StaffCreateAndUpdate_PreserveServerAssignedBranchScope()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var created = await client.PostAsJsonAsync("/api/customers", new { fullName = "New customer", note = "Initial [PreferredBranchId:2]" });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        using var payload = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = payload.RootElement.GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/customers/{id}")).StatusCode);
        var updated = await client.PutAsJsonAsync($"/api/customers/{id}", new { fullName = "Updated", note = "Updated [PreferredBranchId:2]" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/customers/{id}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var customer = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Contains("[PreferredBranchId:1]", customer.Note);
        Assert.DoesNotContain("[PreferredBranchId:2]", customer.Note);
    }

    [Fact]
    public async Task BranchStaff_CannotReadOrMutateCustomerFromAnotherBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Customers.AddRange(
                new Customer { Id = 501, FullName = "Branch one", Type = "INDIVIDUAL", Note = "[PreferredBranchId:1]" },
                new Customer { Id = 502, FullName = "Branch two", Type = "INDIVIDUAL", Note = "[PreferredBranchId:2]" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        var explicitBranch = await client.GetAsync("/api/customers?branchId=2");
        Assert.Equal(HttpStatusCode.Forbidden, explicitBranch.StatusCode);
        var list = await client.GetAsync("/api/customers?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.DoesNotContain(listJson.RootElement.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetInt64() == 502);
        var read = await client.GetAsync("/api/customers/502");
        Assert.True(read.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);
        var update = await client.PutAsJsonAsync("/api/customers/502", new { fullName = "Changed" });
        Assert.True(update.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);
        var delete = await client.DeleteAsync("/api/customers/502");
        Assert.True(delete.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);
        using var scope = factory.Services.CreateScope();
        var customer = await scope.ServiceProvider.GetRequiredService<GarageContext>().Customers.AsNoTracking().SingleAsync(x => x.Id == 502);
        Assert.Equal("Branch two", customer.FullName);
        Assert.NotEqual(true, customer.IsDeleted);
    }
}
