using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Support;
using Xunit;

namespace MyGarage.Tests;

public sealed class RetailStockConcurrencySqlTests
{
    [LocalSqlFact]
    public async Task ConcurrentStockCommit_ForSameOrder_DoesNotDeductOrRecordTwice()
    {
        var name = "MyGarageStockTest_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true };
        var test = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name, Pooling = false };
        await using var masterConnection = new SqlConnection(master.ConnectionString);
        await masterConnection.OpenAsync();
        await using (var create = masterConnection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(test.ConnectionString).Options;
            await using var seed = new GarageContext(options);
            var script = seed.Database.GenerateCreateScript();
            script = Regex.Replace(script, @"CREATE UNIQUE INDEX \[UQ_ServiceOrder_DiscountCategory\][\s\S]*?;", "", RegexOptions.IgnoreCase);
            script = script.Replace("ON DELETE CASCADE", "ON DELETE NO ACTION", StringComparison.OrdinalIgnoreCase);
            script = Regex.Replace(script, @"ALTER TABLE\s+\[[^\]]+\]\s+ADD CONSTRAINT\s+\[[^\]]+\]\s+FOREIGN KEY[\s\S]*?;", "", RegexOptions.IgnoreCase);
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                if (!string.IsNullOrWhiteSpace(batch)) await seed.Database.ExecuteSqlRawAsync(batch);
            var branch = new Branch { Name = "Branch", Status = "ACTIVE" };
            var warehouse = new Warehouse { Branch = branch, Name = "Retail", Code = "W", Type = "RETAIL" };
            var product = new Product { Name = "Part", Sku = "P", Unit = "unit", IsActive = true };
            var order = new RetailOrder { Branch = branch, OrderCode = "CONCURRENT", Status = "CONFIRMED", RetailOrderItems = new List<RetailOrderItem> { new() { Product = product, Quantity = 1m } } };
            seed.AddRange(order, warehouse, new Inventory { Warehouse = warehouse, Product = product, Quantity = 2m });
            await seed.SaveChangesAsync();
            var concurrentOptions = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(test.ConnectionString).Options;
            await using var a = new GarageContext(concurrentOptions);
            await using var b = new GarageContext(concurrentOptions);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task Run(GarageContext db) { await start.Task; await RetailOrderStockCommitter.EnsureStockDeductedAsync(db, order.Id); }
            var first = Run(a); var second = Run(b);
            start.SetResult();
            await Task.WhenAll(first, second);
            seed.ChangeTracker.Clear();
            Assert.Equal(1m, await seed.Inventories.Select(x => x.Quantity).SingleAsync());
            Assert.Equal(1m, await seed.StockTransactions.Where(x => x.RetailOrderId == order.Id && x.Type == "OUT").SumAsync(x => x.Quantity));
            var unavailable = new Product { Name = "Unavailable", Sku = "NONE", Unit = "unit", IsActive = true };
            var shortageOrder = new RetailOrder { BranchId = order.BranchId, OrderCode = "SHORTAGE",
                RetailOrderItems = new List<RetailOrderItem> {
                    new() { ProductId = product.Id, Quantity = 1m },
                    new() { Product = unavailable, Quantity = 1m }
                } };
            seed.RetailOrders.Add(shortageOrder);
            await seed.SaveChangesAsync();
            await using (var attempt = new GarageContext(concurrentOptions))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => RetailOrderStockCommitter.EnsureStockDeductedAsync(attempt, shortageOrder.Id));
                await attempt.SaveChangesAsync();
            }
            seed.ChangeTracker.Clear();
            Assert.Equal(1m, await seed.Inventories.Select(x => x.Quantity).SingleAsync());
            Assert.False(await seed.StockTransactions.AnyAsync(x => x.RetailOrderId == shortageOrder.Id));
            var otherOrders = new[] {
                new RetailOrder { BranchId = order.BranchId, OrderCode = "R-2", RetailOrderItems = new List<RetailOrderItem> { new() { ProductId = product.Id, Quantity = 1m } } },
                new RetailOrder { BranchId = order.BranchId, OrderCode = "R-3", RetailOrderItems = new List<RetailOrderItem> { new() { ProductId = product.Id, Quantity = 1m } } }
            };
            seed.RetailOrders.AddRange(otherOrders);
            await seed.SaveChangesAsync();
            async Task<bool> TryCommit(long id)
            {
                await using var db = new GarageContext(concurrentOptions);
                try { await RetailOrderStockCommitter.EnsureStockDeductedAsync(db, id); return true; }
                catch (InvalidOperationException) { return false; }
            }
            var competing = await Task.WhenAll(TryCommit(otherOrders[0].Id), TryCommit(otherOrders[1].Id));
            Assert.Single(competing, success => success);
            seed.ChangeTracker.Clear();
            Assert.Equal(0m, await seed.Inventories.Select(x => x.Quantity).SingleAsync());
        }
        finally
        {
            await using var drop = masterConnection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
