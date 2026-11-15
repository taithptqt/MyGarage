using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Payments;
using MyGarage.Data;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataInvoiceMixedSourceSqlTests
{
    [LocalSqlFact]
    public async Task MixedAndDanglingSources_ApplyStaffBranchScopeOnSqlBeforePaging()
    {
        var databaseName = "MyGarageODataInvoice_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = databaseName };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{databaseName}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new GarageContext(options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.ServiceOrders (Id bigint NOT NULL PRIMARY KEY, ExecuteBranchId bigint NOT NULL, CustomerId bigint NOT NULL)");
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.RetailOrders (Id bigint NOT NULL PRIMARY KEY, BranchId bigint NOT NULL, CustomerId bigint NULL)");
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.Invoices (Id bigint NOT NULL PRIMARY KEY, ServiceOrderId bigint NULL, RetailOrderId bigint NULL)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.ServiceOrders VALUES (10, 1, 100)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.RetailOrders VALUES (20, 2, 101)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.Invoices VALUES (30, 10, 20), (31, 999, 20), (32, NULL, 20)");

            var query = new InvoiceQuery(new InvoiceQueryReader(db));
            var branch1 = query.Execute(new InvoiceScope(InvoiceScopeKind.Branch, 1));
            var branch2 = query.Execute(new InvoiceScope(InvoiceScopeKind.Branch, 2));
            Assert.Equal(new long[] { 30 }, await branch1.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
            Assert.Equal(1, await branch2.CountAsync());
            Assert.Equal(new long[] { 32 }, await branch2.OrderBy(x => x.Id).Take(1).Select(x => x.Id).ToArrayAsync());
            Assert.Empty(query.Execute(new InvoiceScope(InvoiceScopeKind.Branch, null)));
            Assert.Equal(new long[] { 30, 31, 32 }, await query.Execute(new InvoiceScope(InvoiceScopeKind.Customer, 101)).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
