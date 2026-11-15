using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class PurchaseOrderPaginationTests
{
    [Fact]
    public async Task GoodsReceipts_UsesSamePageLimitForRowsAndMetadata_AndHandlesHugePage()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Suppliers.Add(new Supplier { Id = 11, SupplierName = "Supplier" });
            db.Warehouses.Add(new Warehouse { Id = 12, BranchId = 1, Code = "W1", Name = "Warehouse", Type = "GENERAL" });
            db.PurchaseOrders.Add(new PurchaseOrder { Id = 1, BranchId = 1, SupplierId = 11, DestinationWarehouseId = 12, OrderDate = new DateOnly(2026, 1, 1), Status = "APPROVED" });
            for (var i = 1; i <= 110; i++)
                db.GoodsReceiptNotes.Add(new GoodsReceiptNote { Id = i, PurchaseOrderId = 1, WarehouseId = 12, GrnCode = $"GRN-{i}", Status = "POSTED", ReceivedAt = new DateTime(2026, 1, 1) });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        using var response = await client.GetAsync("/api/v1/goods-receipts?page=1&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(100, body.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(110, body.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(100, body.RootElement.GetProperty("items").GetArrayLength());
        using var huge = await client.GetAsync("/api/v1/goods-receipts?page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, huge.StatusCode);
        using var hugeBody = JsonDocument.Parse(await huge.Content.ReadAsStringAsync());
        Assert.Empty(hugeBody.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(110, hugeBody.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task List_EnvelopesPagesAndGroupsStatesWithinStaffBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Suppliers.Add(new Supplier { Id = 11, SupplierName = "Supplier" });
            db.Warehouses.Add(new Warehouse { Id = 12, BranchId = 1, Code = "W1", Name = "Warehouse", Type = "GENERAL" });
            for (var i = 1; i <= 65; i++)
                db.PurchaseOrders.Add(new PurchaseOrder
                {
                    Id = i, BranchId = 1, SupplierId = 11, DestinationWarehouseId = 12,
                    OrderDate = new DateOnly(2026, 1, 1), CreatedAt = new DateTime(2026, 1, 1),
                    Status = i <= 60 ? "PENDING" : i == 61 ? "APPROVED" : i == 62 ? "PARTIAL_RECEIVED" : i == 63 ? "COMPLETED" : "RECEIVED"
                });
            db.PurchaseOrders.Add(new PurchaseOrder { Id = 66, BranchId = 2, SupplierId = 11,
                DestinationWarehouseId = 12, OrderDate = new DateOnly(2026, 1, 1), Status = "PENDING" });
            db.PurchaseOrders.Add(new PurchaseOrder { Id = 67, BranchId = 1, SupplierId = 11,
                DestinationWarehouseId = 12, OrderDate = new DateOnly(2026, 1, 1), Status = "PARTIALLY_RECEIVED" });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        var response = await client.GetAsync("/api/v1/purchase-orders?statusGroup=PENDING&envelope=true&page=2&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(60, body.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("totalPages").GetInt32());
        Assert.Equal(10, body.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(10, body.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());

        using var oversized = await client.GetAsync("/api/v1/purchase-orders?statusGroup=PENDING&page=1&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, oversized.StatusCode);
        using var oversizedBody = JsonDocument.Parse(await oversized.Content.ReadAsStringAsync());
        Assert.Equal(100, oversizedBody.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(60, oversizedBody.RootElement.GetProperty("items").GetArrayLength());
        using var zeroSize = await client.GetAsync("/api/v1/purchase-orders?statusGroup=PENDING&page=1&pageSize=0");
        Assert.Equal(HttpStatusCode.OK, zeroSize.StatusCode);
        using var zeroBody = JsonDocument.Parse(await zeroSize.Content.ReadAsStringAsync());
        Assert.Equal(50, zeroBody.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(50, zeroBody.RootElement.GetProperty("items").GetArrayLength());
        using var huge = await client.GetAsync("/api/v1/purchase-orders?statusGroup=PENDING&page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, huge.StatusCode);
        using var hugeBody = JsonDocument.Parse(await huge.Content.ReadAsStringAsync());
        Assert.Empty(hugeBody.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(60, hugeBody.RootElement.GetProperty("total").GetInt32());

        foreach (var (group, expected) in new[] { ("APPROVED", 3), ("RECEIVED", 3) })
        {
            var groupResponse = await client.GetAsync($"/api/v1/purchase-orders?statusGroup={group}&envelope=true");
            Assert.Equal(HttpStatusCode.OK, groupResponse.StatusCode);
            using var groupBody = JsonDocument.Parse(await groupResponse.Content.ReadAsStringAsync());
            Assert.Equal(expected, groupBody.RootElement.GetProperty("total").GetInt32());
        }

        var legacy = await client.GetAsync("/api/v1/purchase-orders?status=COMPLETED&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        using var legacyBody = JsonDocument.Parse(await legacy.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, legacyBody.RootElement.ValueKind);
        Assert.Single(legacyBody.RootElement.GetProperty("items").EnumerateArray());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/v1/purchase-orders?status=COMPLETED&statusGroup=RECEIVED")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/purchase-orders?branchId=2&envelope=true")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/purchase-orders?envelope=true")).StatusCode);
    }
}
