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
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Data;
using MyGarage.Infrastructure.Queries;
using Xunit;

namespace MyGarage.Tests;

public sealed class PurchaseOrderItemPageSqlTests
{
    [LocalSqlFact]
    public async Task HttpPage_UsesSqlBranchScopeBeforeCountAndPaging()
    {
        var database = "MyGaragePurchaseItemPage_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = database };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{database}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using (var db = new GarageContext(options))
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.PurchaseOrders (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NOT NULL)");
                await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.PurchaseOrderItems (Id bigint NOT NULL PRIMARY KEY, PurchaseOrderId bigint NOT NULL, ProductId bigint NOT NULL, OrderedQuantity decimal(18,3) NOT NULL, ReceivedQuantity decimal(18,3) NULL, CostPrice decimal(18,2) NOT NULL)");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.PurchaseOrders VALUES (1,1),(2,2)");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.PurchaseOrderItems VALUES (1,1,10,1,0,10),(2,2,10,2,0,20),(3,1,10,3,0,30)");
            }
            await using var factory = new TestWebApplicationFactory();
            await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
            using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPurchaseOrderItemQueryService>();
                services.AddScoped(_ => new SqlQueryContext(options));
                services.AddScoped<IPurchaseOrderItemQueryService>(sp => new EfPurchaseOrderItemQueryService(sp.GetRequiredService<SqlQueryContext>().Db));
            }));
            using var client = app.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
            using var response = await client.GetAsync("/api/v1/purchase-order-items/page?page=2&pageSize=1");
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(3, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
            using var overflow = await client.GetAsync("/api/v1/purchase-order-items/page?page=2147483647&pageSize=100");
            Assert.Equal(HttpStatusCode.OK, overflow.StatusCode);
            using var empty = JsonDocument.Parse(await overflow.Content.ReadAsStringAsync());
            Assert.Empty(empty.RootElement.GetProperty("items").EnumerateArray());
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class SqlQueryContext(DbContextOptions<GarageContext> options) : IDisposable
    {
        public GarageContext Db { get; } = new(options);
        public void Dispose() => Db.Dispose();
    }
}
