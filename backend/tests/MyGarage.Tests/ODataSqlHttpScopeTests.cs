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

public sealed class ODataSqlHttpScopeTests
{
    [LocalSqlFact]
    public async Task Endpoints_ApplyActorScopeBeforeFilterCountAndPaging_ThroughHttpAndSql()
    {
        var databaseName = "MyGarageODataHttpScope_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master",
            IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false
        };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = databaseName };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE [{databaseName}]";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            await using (var seed = new SqlConnection(target.ConnectionString))
            {
                await seed.OpenAsync();
                await using var command = seed.CreateCommand();
                // Minimal schema exercises query translation and authorization, not migrations or production schema.
                command.CommandText = """
                    CREATE TABLE dbo.ServiceOrders (Id bigint NOT NULL PRIMARY KEY, ExecuteBranchId bigint NOT NULL, CustomerId bigint NOT NULL, VehicleId bigint NOT NULL);
                    CREATE TABLE dbo.Appointments (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NOT NULL, CustomerId bigint NOT NULL, VehicleId bigint NULL);
                    CREATE TABLE dbo.RetailOrders (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NOT NULL, CustomerId bigint NULL);
                    CREATE TABLE dbo.Customers (Id bigint NOT NULL PRIMARY KEY, IsDeleted bit NULL);
                    CREATE TABLE dbo.Staffs (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NULL, IsDeleted bit NULL);
                    CREATE TABLE dbo.Vehicles (Id bigint NOT NULL PRIMARY KEY, IsDeleted bit NULL);
                    CREATE TABLE dbo.CustomerVehicles (Id bigint NOT NULL PRIMARY KEY, CustomerId bigint NOT NULL, VehicleId bigint NOT NULL, IsDeleted bit NULL, IsCurrentOwner bit NULL);
                    CREATE TABLE dbo.Quotations (Id bigint NOT NULL PRIMARY KEY, ServiceOrderId bigint NOT NULL);
                    CREATE TABLE dbo.Warehouses (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NOT NULL);
                    CREATE TABLE dbo.Inventories (Id bigint NOT NULL PRIMARY KEY, WarehouseId bigint NOT NULL);
                    INSERT INTO dbo.Customers VALUES (100, 0), (101, 0), (102, 0);
                    INSERT INTO dbo.Staffs VALUES (100, 2, 0), (200, 1, 0), (201, 1, 0), (202, 2, 0);
                    INSERT INTO dbo.Vehicles VALUES (301, 0), (302, 0), (303, 0), (304, 0), (305, 1);
                    INSERT INTO dbo.ServiceOrders VALUES (401, 1, 100, 301), (402, 2, 101, 302);
                    INSERT INTO dbo.Appointments VALUES (501, 1, 100, 303), (502, 2, 101, 304);
                    INSERT INTO dbo.RetailOrders VALUES (601, 1, 102);
                    INSERT INTO dbo.CustomerVehicles VALUES (701, 100, 301, 0, 1), (702, 100, 303, 1, 1), (703, 100, 304, 0, 0), (704, 100, 305, 0, 1), (705, 101, 302, 0, 1);
                    INSERT INTO dbo.Quotations VALUES (801, 401), (802, 402);
                    INSERT INTO dbo.Warehouses VALUES (901, 1), (902, 2);
                    INSERT INTO dbo.Inventories VALUES (1001, 901), (1002, 902);
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await using var factory = new TestWebApplicationFactory();
            await factory.SeedAsync(db =>
            {
                TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
                db.Staffs.Add(new Staff { Id = 100, FullName = "Overlapping staff ID", Phone = "0901234100", Email = "overlap100@example.com", PasswordHash = "hash", BranchId = 2, Status = "ACTIVE", SecurityStamp = "test-stamp" });
                db.Staffs.Add(new Staff { Id = 202, FullName = "Branch 2", Phone = "0901234202", Email = "scope202@example.com", PasswordHash = "hash", BranchId = 2, Status = "ACTIVE", SecurityStamp = "test-stamp" });
                db.Customers.Add(new Customer { Id = 101, FullName = "Customer 2", Phone = "0901234101", Email = "scope101@example.com", PasswordHash = "hash", Type = "INDIVIDUAL", IsActive = true, SecurityStamp = "test-stamp" });
                return Task.CompletedTask;
            });
            using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => new SqlQueryContext(target.ConnectionString));
                services.RemoveAll<IODataQueryService>();
                services.AddScoped<IODataQueryService>(sp => new ODataQueryService(sp.GetRequiredService<SqlQueryContext>().Context));
            }));
            using var client = app.CreateClient();

            async Task Check(string token, string endpoint, long[] expected, string prefix = "/odata")
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.GetAsync($"{prefix}/{endpoint}?$orderby=Id&$select=Id&$count=true&$top=1");
                var payload = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{prefix}/{endpoint}: {response.StatusCode} {payload}");
                using var json = JsonDocument.Parse(payload);
                Assert.Equal(expected.Length, json.RootElement.GetProperty("@odata.count").GetInt32());
                var page = json.RootElement.GetProperty("value").EnumerateArray().Select(x => x.GetProperty("Id").GetInt64()).ToArray();
                Assert.Equal(expected.Take(1), page);
            }

            var manager1 = TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1);
            var manager2 = TestWebApplicationFactory.CreateJwtToken(202, "Staff", "MANAGER", 2);
            var customer1 = TestWebApplicationFactory.CreateJwtToken(100, "Customer");
            var customer2 = TestWebApplicationFactory.CreateJwtToken(101, "Customer");
            var admin = TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1);
            await Check(manager1, "ServiceOrders", new long[] { 401 });
            await Check(manager2, "ServiceOrders", new long[] { 402 });
            await Check(customer1, "ServiceOrders", new long[] { 401 });
            await Check(customer2, "ServiceOrders", new long[] { 402 });
            await Check(manager1, "Quotations", new long[] { 801 });
            await Check(customer2, "Quotations", new long[] { 802 });
            await Check(manager1, "Appointments", new long[] { 501 });
            await Check(customer2, "Appointments", new long[] { 502 });
            await Check(manager1, "Vehicles", new long[] { 301, 303 });
            await Check(manager2, "Vehicles", new long[] { 302, 304 });
            await Check(customer1, "Vehicles", new long[] { 301 });
            await Check(TestWebApplicationFactory.CreateJwtToken(100, "Staff", "MANAGER", 2), "Vehicles", new long[] { 302, 304 });
            await Check(customer2, "Vehicles", new long[] { 302 });
            await Check(manager1, "Customers", new long[] { 100, 102 });
            await Check(manager2, "Customers", new long[] { 101 });
            await Check(admin, "Customers", new long[] { 100, 101, 102 }, "/api/v1/odata");
            await Check(manager1, "Staffs", new long[] { 200, 201 });
            await Check(manager2, "Staffs", new long[] { 100, 202 });
            await Check(manager1, "Inventory", new long[] { 1001 });
            await Check(manager2, "Inventory", new long[] { 1002 });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customer1);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/odata/Customers")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/odata/Staffs")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/odata/Inventory")).StatusCode);
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class SqlQueryContext : IDisposable
    {
        public SqlQueryContext(string connectionString)
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(connectionString).Options;
            Context = new GarageContext(options);
        }

        public GarageContext Context { get; }
        public void Dispose() => Context.Dispose();
    }
}
