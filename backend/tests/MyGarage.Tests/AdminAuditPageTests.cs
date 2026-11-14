using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class AdminAuditPageTests
{
    [Fact]
    public async Task AdminAudit_FilterPagingAndAuthorizationArePreserved()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.AuditLogs.AddRange(
                new AuditLog { Id = 901, UserId = 201, UserType = "Staff", Action = "UPDATE", TableName = "AuditTestRecords", RecordId = 1, NewValue = "match", Timestamp = new DateTime(2026, 1, 1) },
                new AuditLog { Id = 902, UserId = 201, UserType = "Staff", Action = "UPDATE", TableName = "AuditTestRecords", RecordId = 2, NewValue = "match", Timestamp = new DateTime(2026, 1, 1) },
                new AuditLog { Id = 903, UserId = 201, UserType = "Staff", Action = "CREATE", TableName = "AuditTestRecords", RecordId = 3, NewValue = "other", Timestamp = new DateTime(2026, 1, 2) });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/audit-logs/entries")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit-logs/entries")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var response = await client.GetAsync("/api/audit-logs/entries?tableName=audittest&action=update&query=match&page=2&pageSize=1");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, doc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(901, doc.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        var overflow = await client.GetAsync("/api/audit-logs/admin-changes?tableName=audittest&page=2147483647&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, overflow.StatusCode);
        using var overflowDoc = JsonDocument.Parse(await overflow.Content.ReadAsStringAsync());
        Assert.Empty(overflowDoc.RootElement.GetProperty("items").EnumerateArray());
    }
}
