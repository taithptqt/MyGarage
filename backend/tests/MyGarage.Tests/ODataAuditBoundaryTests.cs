using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataAuditBoundaryTests
{
    [Theory]
    [InlineData("/odata")]
    [InlineData("/api/v1/odata")]
    public async Task Audit_ScopesBeforeCountAndPaging_AndHidesFreeText(string prefix)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.AuditLogs.AddRange(
                Log(9101, 1), Log(9102, 1), Log(9103, 2));
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{prefix}/AuditLogs")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        using var response = await client.GetAsync($"{prefix}/AuditLogs?$filter=TableName%20eq%20'AuditReview'&$orderby=Id%20desc&$count=true&$top=1&$select=Id,OldValue,NewValue");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, payload);
        Assert.DoesNotContain("free-text-secret", payload);
        using var json = JsonDocument.Parse(payload);
        Assert.Equal(2, json.RootElement.GetProperty("@odata.count").GetInt32());
        var row = Assert.Single(json.RootElement.GetProperty("value").EnumerateArray());
        Assert.Equal(9102, row.GetProperty("Id").GetInt64());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("OldValue").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("NewValue").ValueKind);
    }

    [LocalSqlFact]
    public async Task Audit_HttpSql_ScopesBeforeCountAndDoesNotReadHistoricalValues()
    {
        var name = "MyGarageODataAudit_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master",
            IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false
        };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE [{name}]";
            await create.ExecuteNonQueryAsync();
        }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using (var db = new GarageContext(options))
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.AuditLogs (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NULL, TableName nvarchar(100) NOT NULL, OldValue nvarchar(max) NULL, NewValue nvarchar(max) NULL)");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.AuditLogs (Id, BranchId, TableName, OldValue, NewValue) VALUES (9101,1,N'AuditReview',N'free-text-secret',N'free-text-secret'),(9102,1,N'AuditReview',N'free-text-secret',N'free-text-secret'),(9103,2,N'AuditReview',N'free-text-secret',N'free-text-secret')");
            }

            await using var factory = new TestWebApplicationFactory();
            await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
            using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IODataQueryService>();
                services.AddScoped(_ => new SqlAuditContext(options));
                services.AddScoped<IODataQueryService>(sp => new ODataQueryService(sp.GetRequiredService<SqlAuditContext>().Db));
            }));
            using var client = app.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
            foreach (var prefix in new[] { "/odata", "/api/v1/odata" })
            {
                using var response = await client.GetAsync($"{prefix}/AuditLogs?$filter=TableName%20eq%20'AuditReview'&$orderby=Id%20desc&$count=true&$top=1&$select=Id,OldValue,NewValue");
                var payload = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.OK, payload);
                Assert.DoesNotContain("free-text-secret", payload);
                using var json = JsonDocument.Parse(payload);
                Assert.Equal(2, json.RootElement.GetProperty("@odata.count").GetInt32());
                var row = Assert.Single(json.RootElement.GetProperty("value").EnumerateArray());
                Assert.Equal(9102, row.GetProperty("Id").GetInt64());
                Assert.Equal(JsonValueKind.Null, row.GetProperty("OldValue").ValueKind);
                Assert.Equal(JsonValueKind.Null, row.GetProperty("NewValue").ValueKind);
            }
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static AuditLog Log(long id, long branchId) => new()
    {
        Id = id, UserId = 200, UserType = "STAFF", BranchId = branchId,
        Action = "UPDATE", TableName = "AuditReview", RecordId = 1,
        OldValue = "{\"Notes\":\"free-text-secret\"}", NewValue = "free-text-secret"
    };

    private sealed class SqlAuditContext(DbContextOptions<GarageContext> options) : IDisposable
    {
        public GarageContext Db { get; } = new(options);
        public void Dispose() => Db.Dispose();
    }
}
