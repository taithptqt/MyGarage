using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Application.Purchasing.DTOs;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class PurchaseOrderConcurrencySqlTests
{
    [LocalSqlFact]
    public async Task ConcurrentReceipts_PreserveQuantityAndMovingAverageCost()
    {
        var databaseName = "MyGaragePoConcurrency_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true };
        var test = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = databaseName, Pooling = false };
        await using var masterConnection = new SqlConnection(master.ConnectionString);
        await masterConnection.OpenAsync();
        await using (var create = masterConnection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{databaseName}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(test.ConnectionString, sql => sql.EnableRetryOnFailure()).Options;
            long orderId, productId, warehouseId, itemId, actorId;
            await using (var seed = new GarageContext(options, new HttpContextAccessor()))
            {
                // Existing full generated schema has unrelated SQL Server incompatibilities.
                // This isolated test checks transaction behavior, not migration completeness.
                var script = seed.Database.GenerateCreateScript();
                script = Regex.Replace(script, @"CREATE UNIQUE INDEX \[UQ_ServiceOrder_DiscountCategory\][\s\S]*?;", "", RegexOptions.IgnoreCase);
                script = script.Replace("ON DELETE CASCADE", "ON DELETE NO ACTION", StringComparison.OrdinalIgnoreCase);
                script = Regex.Replace(script, @"ALTER TABLE\s+\[[^\]]+\]\s+ADD CONSTRAINT\s+\[[^\]]+\]\s+FOREIGN KEY[\s\S]*?;", "", RegexOptions.IgnoreCase);
                foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                    if (!string.IsNullOrWhiteSpace(batch)) await seed.Database.ExecuteSqlRawAsync(batch);
                var branch = new Branch { Name = "Main", Status = "ACTIVE" };
                var staff = new Staff { FullName = "Test manager", PasswordHash = "hash", Branch = branch, Status = "ACTIVE" };
                var warehouse = new Warehouse { Branch = branch, Code = "W", Name = "Warehouse", Type = "GENERAL", IsActive = true };
                var product = new Product { Sku = "SKU", Name = "Product", Unit = "unit", IsActive = true };
                var supplier = new Supplier { SupplierName = "Supplier" };
                var order = new PurchaseOrder { Branch = branch, DestinationWarehouse = warehouse, Supplier = supplier,
                    Status = "APPROVED", OrderDate = DateOnly.FromDateTime(DateTime.UtcNow) };
                var item = new PurchaseOrderItem { PurchaseOrder = order, Product = product, OrderedQuantity = 10m, ReceivedQuantity = 0m, CostPrice = 200m };
                seed.AddRange(branch, staff, warehouse, product, supplier, order, item);
                seed.Inventories.Add(new Inventory { Warehouse = warehouse, Product = product, Quantity = 10m, CurrentMacPrice = 100m, TotalInventoryValue = 1000m });
                await seed.SaveChangesAsync();
                orderId = order.Id; productId = product.Id; warehouseId = warehouse.Id; itemId = item.Id; actorId = staff.Id;
            }
            var actor = new UserActorContext(actorId, "Staff", "MANAGER", null, true);
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var participants = 0;
            async Task<PurchaseOrderReceiveResultDto> Receive()
            {
                await using var db = new GarageContext(options, new HttpContextAccessor());
                var service = new PurchaseOrderApplicationService(db, new PurchaseOrderAuthorizationService(db),
                    new InventoryAdminService(db), new GrnSnapshotService(db));
                if (Interlocked.Increment(ref participants) == 2) ready.TrySetResult();
                await start.Task;
                return await service.ReceivePurchaseOrderAsync(orderId,
                    new PurchaseOrderReceiveInput(null, null, actorId, new[] { new PurchaseOrderReceiveItemInput(itemId, productId, 7m, 200m) }), actor);
            }
            var first = Receive(); var second = Receive();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            start.SetResult();
            var results = await Task.WhenAll(first, second);
            Assert.Equal(10m, results.Sum(result => result.Lines.Sum(line => line.AcceptedQuantity)));
            await using var verify = new GarageContext(options, new HttpContextAccessor());
            var storedItem = await verify.PurchaseOrderItems.SingleAsync(x => x.Id == itemId);
            var stock = await verify.Inventories.SingleAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId);
            Assert.Equal(10m, storedItem.ReceivedQuantity);
            Assert.Equal(20m, stock.Quantity);
            Assert.Equal(150m, stock.CurrentMacPrice);
            Assert.Equal(2, await verify.GoodsReceiptNotes.CountAsync(x => x.PurchaseOrderId == orderId));
            Assert.Equal(10m, await verify.GoodsReceiptNoteItems.SumAsync(x => x.ReceivedQuantity));

            var existingOrder = await verify.PurchaseOrders.AsNoTracking().SingleAsync(x => x.Id == orderId);
            var newWarehouse = new Warehouse { BranchId = existingOrder.BranchId, Code = "W2", Name = "New warehouse", Type = "GENERAL", IsActive = true };
            var secondOrder = new PurchaseOrder { BranchId = existingOrder.BranchId, DestinationWarehouse = newWarehouse,
                SupplierId = existingOrder.SupplierId, Status = "APPROVED", OrderDate = existingOrder.OrderDate };
            var secondItem = new PurchaseOrderItem { PurchaseOrder = secondOrder, ProductId = productId,
                OrderedQuantity = 10m, ReceivedQuantity = 0m, CostPrice = 100m };
            verify.AddRange(secondOrder, secondItem);
            await verify.SaveChangesAsync();
            var secondService = new PurchaseOrderApplicationService(verify, new PurchaseOrderAuthorizationService(verify),
                new InventoryAdminService(verify), new GrnSnapshotService(verify));
            await secondService.ReceivePurchaseOrderAsync(secondOrder.Id,
                new PurchaseOrderReceiveInput(null, null, actorId, new[] {
                    new PurchaseOrderReceiveItemInput(secondItem.Id, productId, 2m, 100m),
                    new PurchaseOrderReceiveItemInput(secondItem.Id, productId, 3m, 200m) }), actor);
            verify.ChangeTracker.Clear();
            var newStock = await verify.Inventories.Where(x => x.ProductId == productId && x.WarehouseId == newWarehouse.Id).ToListAsync();
            Assert.Single(newStock);
            Assert.Equal(5m, newStock[0].Quantity);
            Assert.Equal(160m, newStock[0].CurrentMacPrice);
        }
        finally
        {
            await using var drop = masterConnection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
