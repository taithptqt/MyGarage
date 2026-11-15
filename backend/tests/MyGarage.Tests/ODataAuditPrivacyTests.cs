using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Common.Audit;
using MyGarage.Data;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataAuditPrivacyTests
{
    [Fact]
    public void HistoricalAuditRead_OnlyReturnsApprovedBooleanFields()
    {
        var safe = AuditValueRedactor.RedactForRead("{\"Notes\":\"token=secret\",\"Payload\":\"[{\\\"password\\\":\\\"secret\\\"}]\",\"IsDeleted\":true,\"IsActive\":\"secret\"}");
        Assert.NotNull(safe);
        using var json = JsonDocument.Parse(safe);
        Assert.True(json.RootElement.GetProperty("IsDeleted").GetBoolean());
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.Null(AuditValueRedactor.RedactForRead("{\"Notes\":\"secret\"}"));
        Assert.Null(AuditValueRedactor.RedactForRead("{\"IsDeleted\":true,\"isdeleted\":false}"));
    }
    [Fact]
    public void AuditValueRedactor_SanitizesEncodedArraysAndRejectsMalformedOrExcessiveNesting()
    {
        var embedded = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object> { ["PasswordHash"] = "array-secret", ["Status"] = "ACTIVE" }
        });
        var value = JsonSerializer.Serialize(new Dictionary<string, object> { ["Payload"] = embedded });
        var safe = AuditValueRedactor.Redact(value);
        Assert.NotNull(safe);
        Assert.DoesNotContain("array-secret", safe);
        using var document = JsonDocument.Parse(safe);
        using var array = JsonDocument.Parse(document.RootElement.GetProperty("Payload").GetString()!);
        Assert.Equal("[REDACTED]", array.RootElement[0].GetProperty("PasswordHash").GetString());
        Assert.Equal("ACTIVE", array.RootElement[0].GetProperty("Status").GetString());

        Assert.Null(AuditValueRedactor.Redact("{\"Payload\":\"[{broken]\"}"));
        var nested = "{}";
        for (var i = 0; i < 40; i++) nested = "{\"Payload\":" + nested + "}";
        Assert.Null(AuditValueRedactor.Redact(nested));
        Assert.Null(AuditValueRedactor.Redact("{\"Value\":1,\"Value\":2}"));
    }

    [Fact]
    public void AuditValueRedactor_RemovesSensitiveNestedFields_AndRejectsUnknownFormats()
    {
        var safe = AuditValueRedactor.Redact("{\"FullName\":\"An\",\"Nested\":[{\"PasswordHash\":\"secret\",\"Status\":\"ACTIVE\"}],\"TokenHash\":\"token-secret\",\"Authorization\":\"bearer-secret\"}");
        Assert.NotNull(safe);
        Assert.Contains("ACTIVE", safe);
        Assert.Contains("An", safe);
        Assert.DoesNotContain("token-secret", safe);
        Assert.DoesNotContain("bearer-secret", safe);
        Assert.DoesNotContain("\"secret\"", safe);
        Assert.Null(AuditValueRedactor.Redact("legacy-secret"));
        Assert.Null(AuditValueRedactor.Redact("{broken"));
    }

    [Fact]
    public async Task AuditOData_DoesNotExposeHistoricalValues_EvenWithSelectOrSingleRecord()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.AuditLogs.Add(new AuditLog
            {
                Id = 9001, UserId = 200, UserType = "STAFF", BranchId = 1,
                Action = "UPDATE", TableName = "Customers", RecordId = 100,
                OldValue = "{\"PasswordHash\":\"old-secret\"}",
                NewValue = "{\"SecurityStamp\":\"new-secret\"}"
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        foreach (var path in new[]
        {
            "/odata/AuditLogs?$filter=Id%20eq%209001&$select=Id,OldValue,NewValue",
            "/odata/AuditLogs/9001?$select=Id,OldValue,NewValue"
        })
        {
            using var response = await client.GetAsync(path);
            var payload = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, payload);
            Assert.DoesNotContain("old-secret", payload);
            Assert.DoesNotContain("new-secret", payload);
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            var row = root.TryGetProperty("value", out var rows) ? rows.EnumerateArray().Single() : root;
            Assert.Equal(9001, row.GetProperty("Id").GetInt64());
            Assert.Equal(JsonValueKind.Null, row.GetProperty("OldValue").ValueKind);
            Assert.Equal(JsonValueKind.Null, row.GetProperty("NewValue").ValueKind);
        }

        using var restResponse = await client.GetAsync("/api/v1/audit-logs/9001");
        var restPayload = await restResponse.Content.ReadAsStringAsync();
        Assert.True(restResponse.StatusCode == HttpStatusCode.OK, restPayload);
        Assert.DoesNotContain("old-secret", restPayload);
        Assert.DoesNotContain("new-secret", restPayload);

        using var entriesResponse = await client.GetAsync("/api/v1/audit-logs/entries?tableName=Customers");
        var entriesPayload = await entriesResponse.Content.ReadAsStringAsync();
        Assert.True(entriesResponse.StatusCode == HttpStatusCode.OK, entriesPayload);
        Assert.DoesNotContain("old-secret", entriesPayload);
        Assert.DoesNotContain("new-secret", entriesPayload);
        using var searchResponse = await client.GetAsync("/api/v1/audit-logs/entries?tableName=Customers&query=new-secret");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        using var searchJson = JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, searchJson.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task AuditWrite_ExcludesSensitiveProperties_ButKeepsOtherChanges()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Customers.Add(new Customer
            {
                Id = 777, FullName = "Audit Test", Phone = "0900000777", Type = "INDIVIDUAL",
                PasswordHash = "secret-hash", SecurityStamp = "secret-stamp"
            });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var audit = await db.AuditLogs.SingleAsync(x => x.NewValue != null && x.NewValue.Contains("Audit Test"));
        Assert.Contains("Audit Test", audit.NewValue);
        Assert.DoesNotContain("secret-hash", audit.NewValue);
        Assert.DoesNotContain("secret-stamp", audit.NewValue);
    }

    [Fact]
    public async Task AuditWrite_SensitiveOnlyUpdate_KeepsEventWithoutSecretValues()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Customers.Add(new Customer
            {
                Id = 778, FullName = "Audit Only", Phone = "0900000778", Type = "INDIVIDUAL",
                PasswordHash = "old-secret-hash"
            });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var customer = await db.Customers.SingleAsync(x => x.FullName == "Audit Only");
        customer.PasswordHash = "new-secret-hash";
        await db.SaveChangesAsync();

        var audit = await db.AuditLogs.SingleAsync(x => x.Action == "UPDATE");
        Assert.Equal("UPDATE", audit.Action);
        Assert.DoesNotContain("secret-hash", audit.OldValue ?? string.Empty);
        Assert.DoesNotContain("secret-hash", audit.NewValue ?? string.Empty);
        Assert.DoesNotContain("PasswordHash", audit.OldValue ?? string.Empty);
        Assert.DoesNotContain("PasswordHash", audit.NewValue ?? string.Empty);
    }
}
