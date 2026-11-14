using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class RetailReturnScopeTests
{
    [Fact]
    public async Task Finalize_DoesNotRewriteInvoicePaidAmountWithoutPaymentTransaction()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Warehouses.Add(new Warehouse { Id = 1, BranchId = 1, Name = "W1", Code = "W1", Type = "GENERAL" });
            db.RetailOrders.Add(new RetailOrder { Id = 10, BranchId = 1, OrderCode = "R1", FinalAmount = 100 });
            db.RetailOrderItems.Add(new RetailOrderItem { Id = 20, RetailOrderId = 10, ProductId = 1, Quantity = 2 });
            db.RetailReturns.Add(new RetailReturn { Id = 31, RetailOrderId = 10, ReturnCode = "T2", Status = "PENDING", CreatedBy = 200 });
            db.RetailReturnItems.Add(new RetailReturnItem { Id = 40, RetailReturnId = 31, RetailOrderItemId = 20,
                WarehouseId = 1, ReturnedQuantity = 1, RefundAmount = 10, ItemCondition = "GOOD", CreatedBy = 200 });
            db.Invoices.Add(new Invoice { Id = 50, RetailOrderId = 10, TotalAmount = 100, PaidAmount = 120, PayerType = "CUSTOMER" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var result = await client.PostAsync("/api/retail-returns/31/finalize", null);
        Assert.True(result.StatusCode == HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal(120, (await db.Invoices.SingleAsync(x => x.Id == 50)).PaidAmount);
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task Create_ReturnAndItem_UseAuthenticatedAuditActor()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Warehouses.Add(new Warehouse { Id = 1, BranchId = 1, Name = "W1", Code = "W1", Type = "GENERAL" });
            db.RetailOrders.Add(new RetailOrder { Id = 10, BranchId = 1, OrderCode = "R1", FinalAmount = 100 });
            db.RetailOrderItems.Add(new RetailOrderItem { Id = 20, RetailOrderId = 10, ProductId = 1, Quantity = 2 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var created = await client.PostAsJsonAsync("/api/retail-returns", new { retailOrderId = 10, returnCode = "RET-1", createdBy = 999, updatedBy = 999, approvedBy = 999 });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var ticket = await db.RetailReturns.SingleAsync();
        Assert.Equal(200, ticket.CreatedBy);
        Assert.Null(ticket.UpdatedBy);
        Assert.Null(ticket.ApprovedBy);
        var item = await client.PostAsJsonAsync("/api/retail-return-items", new { retailReturnId = ticket.Id, retailOrderItemId = 20, warehouseId = 1, returnedQuantity = 1, refundAmount = 10, itemCondition = "GOOD", createdBy = 999, updatedBy = 999 });
        Assert.True(item.StatusCode == HttpStatusCode.Created, await item.Content.ReadAsStringAsync());
        var savedLine = await db.RetailReturnItems.AsNoTracking().SingleAsync();
        Assert.Equal(200, savedLine.CreatedBy);
        Assert.Null(savedLine.UpdatedBy);
    }

    [Fact]
    public async Task Finalize_RejectsCrossBranchAndForeignOrderLines()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Warehouses.AddRange(
                new Warehouse { Id = 1, BranchId = 1, Name = "W1", Code = "W1", Type = "GENERAL" },
                new Warehouse { Id = 2, BranchId = 2, Name = "W2", Code = "W2", Type = "GENERAL" });
            db.RetailOrders.AddRange(
                new RetailOrder { Id = 10, BranchId = 1, OrderCode = "R1", FinalAmount = 100 },
                new RetailOrder { Id = 11, BranchId = 2, OrderCode = "R2", FinalAmount = 100 });
            db.RetailOrderItems.AddRange(
                new RetailOrderItem { Id = 20, RetailOrderId = 10, ProductId = 1, Quantity = 2 },
                new RetailOrderItem { Id = 21, RetailOrderId = 11, ProductId = 1, Quantity = 2 });
            db.RetailReturns.AddRange(
                new RetailReturn { Id = 30, RetailOrderId = 11, ReturnCode = "T1", Status = "PENDING", CreatedBy = 200 },
                new RetailReturn { Id = 31, RetailOrderId = 10, ReturnCode = "T2", Status = "PENDING", CreatedBy = 200 });
            db.RetailReturnItems.AddRange(
                new RetailReturnItem { Id = 40, RetailReturnId = 31, RetailOrderItemId = 21,
                    WarehouseId = 1, ReturnedQuantity = 1, ItemCondition = "GOOD", CreatedBy = 200 },
                new RetailReturnItem { Id = 41, RetailReturnId = 30, RetailOrderItemId = 21,
                    WarehouseId = 2, ReturnedQuantity = 1, ItemCondition = "GOOD", CreatedBy = 200 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        using (var seedScope = factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<GarageContext>();
            Assert.Equal(2, await seedDb.RetailReturns.Where(x => x.Id == 30).Select(x => x.RetailOrder.BranchId).SingleAsync());
            long? branch = 1;
            Assert.Null(await seedDb.RetailReturns.AsNoTracking().Where(x => x.Id == 30 && (!branch.HasValue || x.RetailOrder.BranchId == branch.Value)).Select(x => (long?)x.RetailOrderId).FirstOrDefaultAsync());
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/purchase-orders?branchId=2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/retail-returns/30")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/retail-returns", new { retailOrderId = 11, status = "PENDING" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/retail-returns", new { retailOrderId = 10, status = "COMPLETED" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/retail-returns/31", new { retailOrderId = 10, status = "COMPLETED" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/retail-return-items/41")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/retail-return-items/41")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/retail-return-items", new { retailReturnId = 30, retailOrderItemId = 21, warehouseId = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/retail-return-items", new { retailReturnId = 31, retailOrderItemId = 21, warehouseId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/retail-return-items", new { retailReturnId = 31, retailOrderItemId = 20, warehouseId = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/retail-return-items", new { retailReturnId = 31, retailOrderItemId = 20, warehouseId = 1, status = "COMPLETED" })).StatusCode);
        var foreign = await client.PostAsync("/api/retail-returns/30/finalize", null);
        Assert.True(foreign.StatusCode == HttpStatusCode.NotFound, await foreign.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/retail-returns/31/finalize", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal("PENDING", (await db.RetailReturns.SingleAsync(x => x.Id == 31)).Status);
        Assert.Equal(100, (await db.RetailOrders.SingleAsync(x => x.Id == 10)).FinalAmount);
        Assert.Empty(await db.StockTransactions.ToListAsync());
    }

    [Fact]
    public async Task Finalize_RejectsRefundGreaterThanRemainingOrderAmount()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Warehouses.Add(new Warehouse { Id = 1, BranchId = 1, Name = "W1", Code = "W1", Type = "GENERAL" });
            db.RetailOrders.Add(new RetailOrder { Id = 10, BranchId = 1, OrderCode = "R1", FinalAmount = 100 });
            db.RetailOrderItems.Add(new RetailOrderItem { Id = 20, RetailOrderId = 10, ProductId = 1, Quantity = 2 });
            db.RetailReturns.Add(new RetailReturn { Id = 31, RetailOrderId = 10, ReturnCode = "T2", Status = "PENDING", CreatedBy = 200 });
            db.RetailReturnItems.Add(new RetailReturnItem { Id = 40, RetailReturnId = 31, RetailOrderItemId = 20,
                WarehouseId = 1, ReturnedQuantity = 1, RefundAmount = 101, ItemCondition = "GOOD", CreatedBy = 200 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/retail-returns/31/finalize", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal("PENDING", (await db.RetailReturns.SingleAsync(x => x.Id == 31)).Status);
        Assert.Equal(100, (await db.RetailOrders.SingleAsync(x => x.Id == 10)).FinalAmount);
        Assert.Empty(await db.StockTransactions.ToListAsync());
    }
}
