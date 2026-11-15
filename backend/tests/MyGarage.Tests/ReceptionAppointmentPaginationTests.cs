using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class ReceptionAppointmentPaginationTests
{
    [Fact]
    public async Task ReceptionPage_FiltersBeforePaging_AndKeepsGroupsTogetherAndBranchScoped()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Customers.Add(new Customer { Id = 9010, FullName = "Paging customer", Phone = "090123", Type = "INDIVIDUAL" });
            for (var i = 1; i <= 3; i++)
                db.GroupSessions.Add(new GroupSession { Id = 9200 + i, BranchId = 1, CustomerId = 9010, GroupCode = $"GROUP-{i}", Status = "PENDING" });
            for (var i = 1; i <= 6; i++)
                db.Appointments.Add(new Appointment { Id = 9300 + i, BranchId = 1, CustomerId = 9010,
                    GroupSessionId = 9200 + (i + 1) / 2, Status = "PENDING", StartTime = new DateTime(2026, 11, 13, 8, 0, 0),
                    EndTime = new DateTime(2026, 11, 13, 9, 0, 0), CreatedAt = new DateTime(2026, 11, 10).AddMinutes(i) });
            for (var i = 1; i <= 3; i++)
                db.Appointments.Add(new Appointment { Id = 9400 + i, BranchId = i == 3 ? 2 : 1, CustomerId = 9010,
                    Status = "CONFIRMED", StartTime = new DateTime(2026, 11, 13, 8, 0, 0), EndTime = new DateTime(2026, 11, 13, 9, 0, 0),
                    CreatedAt = new DateTime(2026, 11, 10).AddMinutes(i) });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/v1/appointments/reception-list?dateFrom=2026-11-13&dateTo=2026-11-13&mode=GROUP&pageSize=1&page=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("singleCount").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("groupCount").GetInt32());
        var items = json.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(items[0].GetProperty("groupSessionId").GetInt64(), items[1].GetProperty("groupSessionId").GetInt64());
        var filtered = await client.GetAsync("/api/v1/appointments/reception-list?status=CONFIRMED&search=090123&pageSize=1&page=2");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        using var filteredJson = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        Assert.Equal(2, filteredJson.RootElement.GetProperty("singleCount").GetInt32());
        Assert.Equal(0, filteredJson.RootElement.GetProperty("groupCount").GetInt32());
        Assert.Single(filteredJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/appointments/reception-list?branchId=2")).StatusCode);
    }
}
