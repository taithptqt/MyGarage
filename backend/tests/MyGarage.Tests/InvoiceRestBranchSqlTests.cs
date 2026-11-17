using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Repositories;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceRestBranchSqlTests
{
    [LocalSqlFact]
    public async Task InvoiceListAndDetail_EnforceBranchScopeBeforePagination_OnSqlServer()
    {
        var databaseName = "MyGarageInvoiceRestScope_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master",
            IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false
        };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = databaseName };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using (var db = new GarageContext(options))
            {
                // Deliberately minimal schema: validates SQL translation and scoping, not migrations.
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE dbo.ServiceOrders (Id bigint NOT NULL PRIMARY KEY, ExecuteBranchId bigint NOT NULL);
                    CREATE TABLE dbo.RetailOrders (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NOT NULL);
                    CREATE TABLE dbo.Invoices (
                        Id bigint NOT NULL PRIMARY KEY, ServiceOrderId bigint NULL, RetailOrderId bigint NULL,
                        VehicleId bigint NULL, Phone varchar(20) NULL, Address nvarchar(500) NULL,
                        InvoiceUrl varchar(500) NULL, CompanyName nvarchar(255) NULL, CompanyAddress nvarchar(500) NULL,
                        ReceiveEmail varchar(255) NULL, TaxCode varchar(50) NULL, SubTotalAmount decimal(18,2) NULL,
                        TaxAmount decimal(18,2) NULL, TotalAmount decimal(18,2) NULL, PayerType varchar(50) NOT NULL,
                        PayerId bigint NULL, PaidAmount decimal(18,2) NULL, PaymentStatus varchar(50) NULL,
                        CreditApprovedByStaffId bigint NULL, CreditNote nvarchar(max) NULL, IssuedAt datetime2 NULL,
                        CreatedAt datetime2 NULL, CreatedBy bigint NULL, UpdatedAt datetime2 NULL, UpdatedBy bigint NULL
                    );
                    INSERT INTO dbo.ServiceOrders (Id, ExecuteBranchId) VALUES (10,1);
                    INSERT INTO dbo.RetailOrders (Id, BranchId) VALUES (20,1),(21,2);
                    INSERT INTO dbo.Invoices (Id, ServiceOrderId, RetailOrderId, PayerType, TotalAmount) VALUES
                        (101,NULL,20,'CUSTOMER',10), (102,NULL,21,'CUSTOMER',20),
                        (103,10,21,'CUSTOMER',30), (104,999,21,'CUSTOMER',40);
                    """);
            }

            await using var factory = new TestWebApplicationFactory();
            await factory.SeedAsync(db =>
            {
                TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
                db.Staffs.Add(new Staff { Id = 202, FullName = "Branch 2", Phone = "0900000202", PasswordHash = "hash", BranchId = 2, Status = "ACTIVE", SecurityStamp = "test-stamp" });
                return Task.CompletedTask;
            });
            using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInvoiceRepository>();
                services.AddScoped(_ => new SqlInvoiceContext(options));
                services.AddScoped<IInvoiceRepository>(sp => new InvoiceRepository(sp.GetRequiredService<SqlInvoiceContext>().Db));
            }));
            using var client = app.CreateClient();

            async Task<(int Total, long[] Ids)> List(string token, string page)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.GetAsync($"/api/v1/invoices?page={page}&pageSize=1");
                var payload = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.OK, payload);
                using var json = JsonDocument.Parse(payload);
                return (json.RootElement.GetProperty("totalCount").GetInt32(),
                    json.RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray());
            }

            var branch1 = TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1);
            var first = await List(branch1, "1");
            Assert.Equal(2, first.Total);
            Assert.Equal(new long[] { 103 }, first.Ids);
            var second = await List(branch1, "2");
            Assert.Equal(new long[] { 101 }, second.Ids);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/invoices/102")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/invoices/104")).StatusCode);

            var branch2 = TestWebApplicationFactory.CreateJwtToken(202, "Staff", "MANAGER", 2);
            var other = await List(branch2, "1");
            Assert.Equal(1, other.Total);
            Assert.Equal(new long[] { 102 }, other.Ids);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/invoices/103")).StatusCode);

            var admin = TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1);
            var all = await List(admin, "1");
            Assert.Equal(4, all.Total);
            Assert.Equal(new long[] { 104 }, all.Ids);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/invoices/103")).StatusCode);
        }
        finally
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class SqlInvoiceContext(DbContextOptions<GarageContext> options) : IDisposable
    {
        public GarageContext Db { get; } = new(options);
        public void Dispose() => Db.Dispose();
    }
}
