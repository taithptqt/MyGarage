using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class StaffAdministrationPaginationTests
{
    [Fact]
    public async Task StaffPage_FiltersRoleAndScopesBranchWhileKeepingGlobalStats()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Roles.Add(new Role { Id = 5, RoleName = "Technician", RoleCode = "TECH", Scope = "BRANCH" });
            for (var i = 1; i <= 65; i++)
                db.Staffs.Add(new Staff { Id = 1000 + i, FullName = $"Employee {i}", PasswordHash = "hash",
                    BranchId = i <= 60 ? 1 : 2, RoleId = 5, Status = i % 2 == 0 ? "ACTIVE" : "INACTIVE" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/v1/staffs/admin-list?search=technician&page=2&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(60, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(62, json.RootElement.GetProperty("totalStaff").GetInt32());
        Assert.Equal(32, json.RootElement.GetProperty("activeStaff").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("rolesUsed").GetInt32());
        var items = json.RootElement.GetProperty("items");
        Assert.Equal(10, items.GetArrayLength());
        Assert.Equal(1011, items[0].GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/staffs/admin-list?branchId=2")).StatusCode);
    }
}
