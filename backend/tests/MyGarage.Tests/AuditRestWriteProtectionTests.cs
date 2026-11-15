using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class AuditRestWriteProtectionTests
{
    [Fact]
    public async Task Admin_CannotCreateUpdateOrDeleteAuditViaHttp()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.AuditLogs.Add(new AuditLog { Id = 9001, UserId = 201, UserType = "STAFF", Action = "CREATE", TableName = "Review", RecordId = 4 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        using var create = await client.PostAsJsonAsync("/api/v1/audit-logs", new { action = "FORGED" });
        using var update = await client.PutAsJsonAsync("/api/v1/audit-logs/9001", new { action = "FORGED" });
        using var delete = await client.DeleteAsync("/api/v1/audit-logs/9001");
        foreach (var response in new[] { create, update, delete })
        {
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            Assert.Contains("GET", response.Content.Headers.Allow);
        }
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal("CREATE", (await db.AuditLogs.SingleAsync(x => x.Id == 9001)).Action);
        Assert.False(await db.AuditLogs.AnyAsync(x => x.Action == "FORGED"));
    }
}
