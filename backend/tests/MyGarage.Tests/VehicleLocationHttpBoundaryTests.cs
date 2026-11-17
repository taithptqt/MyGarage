using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class VehicleLocationHttpBoundaryTests
{
    [Fact]
    public async Task CustomerCanReadGarage_ButTechnicianCannotWriteUnsynchronizedLocation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer", "CUSTOMER"));
        using var read = await client.GetAsync("/api/v1/customers/me/garage-locations");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "TECH", 1));
        using var write = await client.PutAsJsonAsync("/api/v1/technician/vehicle-locations/1", new { bayId = 1, note = "Test" });
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task ConditionLogApi_CannotCreateModifyOrDeleteRepairReports()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleConditionLogs.Add(new VehicleConditionLog { Id = 90, ServiceOrderId = 1, LogType = "REPAIR_3D", DefectsAndImages = "{}" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        using var get = await client.GetAsync("/api/v1/vehicle-condition-logs/90");
        using var delete = await client.DeleteAsync("/api/v1/vehicle-condition-logs/90");
        using var create = await client.PostAsJsonAsync("/api/v1/vehicle-condition-logs", new { serviceOrderId = 1, logType = "REPAIR_3D" });
        using var update = await client.PutAsJsonAsync("/api/v1/vehicle-condition-logs/90", new { serviceOrderId = 1, logType = "CHECK_IN" });
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }
}
