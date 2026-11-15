using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Infrastructure.Approvals;
using MyGarage.Application.Approvals;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace MyGarage.Tests;

public sealed class ApprovalListSqlTests
{
    [LocalSqlFact]
    public async Task Read_UsesExistingTableAndRejectsWrites()
    {
        var name = "MyGarageApprovalRead_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            await using var db = new GarageContext(new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.ApprovalRequests (Id bigint NOT NULL PRIMARY KEY, RequestType varchar(50) NOT NULL, ServiceOrderDiscountId bigint NULL, GatePassId bigint NULL, MaterialReturnId bigint NULL, RequestedBy bigint NOT NULL, ApproverId bigint NULL, Status varchar(20) NULL, Reason nvarchar(max) NULL, RejectReason nvarchar(max) NULL, CreatedAt datetime2 NULL, UpdatedAt datetime2 NULL)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.ApprovalRequests (Id,RequestType,RequestedBy,Reason) VALUES (1,'GATE',1,N'Thử'),(2,'MATERIAL',1,NULL)");
            var reader = new ApprovalListReader(db);
            var first = await reader.ReadAsync(1, 1, default);
            Assert.Equal(2, first.Total);
            Assert.Equal("Thử", Assert.Single(first.Items).Reason);
            Assert.Equal(2, Assert.Single((await reader.ReadAsync(2, 1, default)).Items).Id);
            Assert.Empty((await reader.ReadAsync(int.MaxValue, 100, default)).Items);

            // Exercise the HTTP route through the real Infrastructure reader; the regular
            // HTTP fixture intentionally substitutes an InMemory adapter.
            await using (var factory = new TestWebApplicationFactory())
            {
                await factory.SeedAsync(seed => { TestWebApplicationFactory.SeedBasicBranchAndUsers(seed); return Task.CompletedTask; });
                using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IApprovalListReader>();
                    services.AddScoped(_ => new SqlApprovalContext(target.ConnectionString));
                    services.AddScoped<IApprovalListReader>(sp => new ApprovalListReader(sp.GetRequiredService<SqlApprovalContext>().Db));
                }));
                using var client = app.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
                var response = await client.GetAsync("/api/v1/approval-requests?page=2&pageSize=1");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(response.Headers.Contains("X-Total-Count"));
                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(2, payload.RootElement.GetProperty("totalCount").GetInt32());
                Assert.Equal(2, Assert.Single(payload.RootElement.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
            }
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class SqlApprovalContext(string connectionString) : IDisposable
    {
        public GarageContext Db { get; } = new(new DbContextOptionsBuilder<GarageContext>().UseSqlServer(connectionString).Options);
        public void Dispose() => Db.Dispose();
    }
}
