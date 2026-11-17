using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Data;
using MyGarage.Infrastructure.Inventory;
using MyGarage.Infrastructure.Persistence.Stores.Workshop;
using Xunit;

namespace MyGarage.Tests;

public sealed class QuotationInventoryTransactionSqlTests
{
    [LocalSqlFact]
    public async Task InventoryWorkJoinsQuotationTransactionAndRollsBackOnFailure()
    {
        var name = "MyGarageQuoteStock_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString, sql => sql.EnableRetryOnFailure()).Options;
            await using var db = new GarageContext(options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.Decisions (Id int PRIMARY KEY); CREATE TABLE dbo.StockRequests (Id int PRIMARY KEY); CREATE TABLE dbo.ServiceOrders (Id bigint PRIMARY KEY); CREATE TABLE dbo.Quotations (Id bigint PRIMARY KEY, ServiceOrderId bigint NOT NULL); INSERT INTO dbo.ServiceOrders VALUES (1); INSERT INTO dbo.Quotations VALUES (1,1);");
            var quotationStore = new EfQuotationWorkflowStore(db, new ConfigurationBuilder().Build(), NullLogger<EfQuotationWorkflowStore>.Instance);
            var inventoryStore = new EfInventoryStore(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() => quotationStore.ExecuteTransactionalAsync<int>(async () =>
            {
                await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.Decisions VALUES (1)");
                await inventoryStore.ExecuteInTransactionAsync(async _ =>
                {
                    await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.StockRequests VALUES (1)");
                    throw new InvalidOperationException("warehouse failed");
                });
                return 0;
            }));
            Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Decisions").SingleAsync());
            Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.StockRequests").SingleAsync());

            async Task<int> Decide()
            {
                await using var concurrentDb = new GarageContext(options);
                var store = new EfQuotationWorkflowStore(concurrentDb, new ConfigurationBuilder().Build(), NullLogger<EfQuotationWorkflowStore>.Instance);
                return await store.ExecuteTransactionalAsync(async () =>
                {
                    await store.LockServiceOrderForDecisionAsync(1);
                    var count = await concurrentDb.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Decisions").SingleAsync();
                    if (count > 0) return 0;
                    await Task.Delay(100);
                    await concurrentDb.Database.ExecuteSqlRawAsync("INSERT INTO dbo.Decisions VALUES (1)");
                    return 1;
                });
            }
            var results = await Task.WhenAll(Decide(), Decide());
            Assert.Equal(1, results.Sum());

            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.Inventories (Id bigint PRIMARY KEY, WarehouseId bigint NOT NULL, ProductId bigint NOT NULL, Quantity decimal(18,3) NOT NULL, MinStockLevel decimal(18,3) NULL, MaxStockLevel decimal(18,3) NULL, LocationBin varchar(50) NULL, ReservedQuantity decimal(18,3) NULL, Current_MAC_Price decimal(18,2) NULL, TotalInventoryValue decimal(18,2) NULL); INSERT INTO dbo.Inventories (Id,WarehouseId,ProductId,Quantity,ReservedQuantity) VALUES (1,1,1,1,0);");
            async Task<int> Reserve()
            {
                await using var concurrentDb = new GarageContext(options);
                var store = new EfInventoryStore(concurrentDb);
                return await store.ExecuteInTransactionAsync(async ct =>
                {
                    var inventory = await store.GetTrackedInventoryAsync(1, 1, ct) ?? throw new InvalidOperationException();
                    if (inventory.Quantity - (inventory.ReservedQuantity ?? 0) < 1) return 0;
                    await Task.Delay(100, ct);
                    await concurrentDb.Database.ExecuteSqlRawAsync("UPDATE dbo.Inventories SET ReservedQuantity = ReservedQuantity + 1 WHERE Id = 1", ct);
                    return 1;
                });
            }
            var reservations = await Task.WhenAll(Reserve(), Reserve());
            Assert.Equal(1, reservations.Sum());
            Assert.Equal(1m, await db.Database.SqlQueryRaw<decimal>("SELECT ReservedQuantity AS Value FROM dbo.Inventories WHERE Id = 1").SingleAsync());

            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.MaterialRequestDetails (Id bigint PRIMARY KEY); INSERT INTO dbo.MaterialRequestDetails VALUES (1); CREATE TABLE dbo.__EFMigrationsHistory (MigrationId nvarchar(150) PRIMARY KEY, ProductVersion nvarchar(32) NOT NULL);");
            var migrationScript = db.GetService<IMigrator>().GenerateScript(
                "20261117010000_AddInvoiceDueDate", "20261117020000_AddMaterialRequestReservedQuantity",
                MigrationsSqlGenerationOptions.NoTransactions);
            foreach (var batch in System.Text.RegularExpressions.Regex.Split(migrationScript, @"(?im)^\s*GO\s*$"))
                if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.MaterialRequestDetails WHERE ReservedQuantity IS NULL").SingleAsync());
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
