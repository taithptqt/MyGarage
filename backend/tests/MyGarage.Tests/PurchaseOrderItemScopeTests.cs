using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class PurchaseOrderItemScopeTests
{
    [Fact]
    public async Task GoodsReceiptCollection_AndLegacyHistory_ReturnSameRepresentation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Suppliers.Add(new Supplier { Id = 11, SupplierName = "Supplier" });
            db.Warehouses.Add(new Warehouse { Id = 12, BranchId = 1, Code = "W1", Name = "W1", Type = "GENERAL" });
            db.PurchaseOrders.Add(new PurchaseOrder { Id = 1, BranchId = 1, DestinationWarehouseId = 12, SupplierId = 11, Status = "APPROVED" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        using var canonical = await client.GetAsync("/api/v1/purchase-orders/1/goods-receipts");
        using var legacy = await client.GetAsync("/api/v1/purchase-orders/1/receive-history");
        var canonicalBody = await canonical.Content.ReadAsStringAsync();
        Assert.True(canonical.StatusCode == HttpStatusCode.OK, canonicalBody);
        Assert.Equal(canonical.StatusCode, legacy.StatusCode);
        Assert.Equal(canonicalBody, await legacy.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PagedRoute_ScopesBeforeCountAndUsesStableOrder()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Suppliers.Add(new Supplier { Id = 11, SupplierName = "Supplier" });
            db.Warehouses.AddRange(new Warehouse { Id = 12, BranchId = 1, Code = "W1", Name = "W1", Type = "GENERAL" }, new Warehouse { Id = 13, BranchId = 2, Code = "W2", Name = "W2", Type = "GENERAL" });
            db.Products.Add(new Product { Id = 20, Name = "Part", Sku = "P", Unit = "unit", IsActive = true });
            db.PurchaseOrders.AddRange(new PurchaseOrder { Id = 1, BranchId = 1, DestinationWarehouseId = 12, SupplierId = 11, Status = "APPROVED" }, new PurchaseOrder { Id = 2, BranchId = 2, DestinationWarehouseId = 13, SupplierId = 11, Status = "DRAFT" });
            db.PurchaseOrderItems.AddRange(
                new PurchaseOrderItem { Id = 3, PurchaseOrderId = 1, ProductId = 20, OrderedQuantity = 1 },
                new PurchaseOrderItem { Id = 1, PurchaseOrderId = 1, ProductId = 20, OrderedQuantity = 1 },
                new PurchaseOrderItem { Id = 2, PurchaseOrderId = 2, ProductId = 20, OrderedQuantity = 1 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        using var result = await client.GetAsync("/api/v1/purchase-order-items/page?page=2&pageSize=1");
        var content = await result.Content.ReadAsStringAsync();
        Assert.True(result.StatusCode == HttpStatusCode.OK, content);
        using var json = JsonDocument.Parse(content);
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        using var overflow = await client.GetAsync("/api/v1/purchase-order-items/page?page=2147483647&pageSize=2000");
        Assert.Equal(HttpStatusCode.OK, overflow.StatusCode);
        using var empty = JsonDocument.Parse(await overflow.Content.ReadAsStringAsync());
        Assert.Equal(100, empty.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Empty(empty.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task LegacyRoutes_ProtectBranchAndReceiptQuantities()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Suppliers.Add(new Supplier { Id = 11, SupplierName = "Supplier" });
            db.Warehouses.AddRange(new Warehouse { Id = 12, BranchId = 1, Code = "W1", Name = "W1", Type = "GENERAL" }, new Warehouse { Id = 13, BranchId = 2, Code = "W2", Name = "W2", Type = "GENERAL" });
            db.Products.Add(new Product { Id = 20, Name = "Part", Sku = "P", Unit = "unit", IsActive = true });
            db.PurchaseOrders.AddRange(new PurchaseOrder { Id = 1, BranchId = 1, DestinationWarehouseId = 12, SupplierId = 11, Status = "APPROVED" }, new PurchaseOrder { Id = 2, BranchId = 2, DestinationWarehouseId = 13, SupplierId = 11, Status = "DRAFT" });
            db.PurchaseOrderItems.AddRange(new PurchaseOrderItem { Id = 1, PurchaseOrderId = 1, ProductId = 20, OrderedQuantity = 10, ReceivedQuantity = 2, CostPrice = 10 }, new PurchaseOrderItem { Id = 2, PurchaseOrderId = 2, ProductId = 20, OrderedQuantity = 10, ReceivedQuantity = 0, CostPrice = 10 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        using var list = await client.GetAsync("/api/v1/purchase-order-items");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/purchase-order-items/2")).StatusCode);
        var update = await client.PutAsJsonAsync("/api/v1/purchase-order-items/1", new { purchaseOrderId = 1, productId = 20, orderedQuantity = 10, receivedQuantity = 9, costPrice = 10 });
        Assert.False(update.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/purchase-order-items", new { purchaseOrderId = 1, productId = 20, orderedQuantity = 1, receivedQuantity = 1, costPrice = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync("/api/v1/purchase-order-items/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/v1/purchase-order-items/2")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal(2m, db.PurchaseOrderItems.Single(x => x.Id == 1).ReceivedQuantity);
    }
}
