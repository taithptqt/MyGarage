using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class InventoryCatalogPagingTests
{
    [Fact]
    public async Task Catalog_PagesAndSummariesAndExportRemainComplete()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Categories.AddRange(new Category { Id = 101, CategoryName = "Parent" },
                new Category { Id = 102, CategoryName = "Child", ParentId = 101 });
            db.Suppliers.Add(new Supplier { Id = 101, SupplierName = "Supplier" });
            for (var i = 1; i <= 25; i++)
                db.Products.Add(new Product { Id = i, Sku = $"SKU{i}", Name = $"Part {i}", Unit = "piece",
                    CategoryId = i <= 15 ? 102 : null });
            db.ProductImages.Add(new ProductImage { Id = 11, ProductId = 11, ImageUrl = "/image.jpg", IsMain = true });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var result = await client.GetAsync("/api/v1/inventory-management/catalog/products?page=2&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        Assert.Equal(25, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(15, json.RootElement.GetProperty("summary").GetProperty("classified").GetInt32());
        var first = json.RootElement.GetProperty("items")[0];
        Assert.Equal(11, first.GetProperty("item").GetProperty("id").GetInt64());
        Assert.Equal("Child", first.GetProperty("categoryName").GetString());
        Assert.Equal("/image.jpg", first.GetProperty("thumbnail").GetString());
        var categories = await client.GetAsync("/api/v1/inventory-management/catalog/categories?page=1&pageSize=10");
        using var catJson = JsonDocument.Parse(await categories.Content.ReadAsStringAsync());
        var child = catJson.RootElement.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("item").GetProperty("id").GetInt64() == 102);
        Assert.Equal("Parent", child.GetProperty("parentName").GetString());
        Assert.Equal(15, child.GetProperty("productCount").GetInt32());
        var csv = await client.GetStringAsync("/api/v1/inventory-management/catalog/products/export");
        Assert.Contains("\"SKU25\"", csv);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/inventory-management/catalog/products")).StatusCode);
    }
}
