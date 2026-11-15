using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class StockHistoryPaginationTests
{
    [Fact]
    public async Task History_PagesAndSearchesWithinStaffBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Products.Add(new Product { Id = 9, Sku = "TEST-SKU", Name = "Test part", Unit = "unit" });
            db.Warehouses.AddRange(
                new Warehouse { Id = 10, BranchId = 1, Name = "One", Code = "ONE", Type = "GENERAL" },
                new Warehouse { Id = 20, BranchId = 2, Name = "Two", Code = "TWO", Type = "GENERAL" });
            for (var i = 1; i <= 65; i++)
                db.StockTransactions.Add(new StockTransaction { Id = i, ProductId = 9,
                    WarehouseId = i <= 60 ? 10 : 20, Type = "IN", Quantity = 1,
                    CreatedAt = new DateTime(2026, 1, 1), CreatedBy = 200 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/v1/inventory/history?search=TEST-SKU&page=2&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(60, doc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(10, doc.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(10, doc.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        var extreme = await client.GetAsync("/api/v1/inventory/history?page=2147483647&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, extreme.StatusCode);
        using var empty = JsonDocument.Parse(await extreme.Content.ReadAsStringAsync());
        Assert.Equal(0, empty.RootElement.GetProperty("items").GetArrayLength());
    }
}
