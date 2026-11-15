using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class InventoryPartsPageTests
{
    [Fact]
    public async Task PartsPage_FiltersBeforePaging_AndIncludesCategoryAndMainImage()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Categories.Add(new Category { Id = 10, CategoryName = "Parts" });
            for (var i = 1; i <= 40; i++)
                db.Products.Add(new Product { Id = i, Name = $"Part {i}", Sku = $"SKU-{i:D2}", Unit = "unit", CategoryId = 10 });
            db.ProductImages.AddRange(
                new ProductImage { Id = 1, ProductId = 31, ImageUrl = "secondary", IsMain = false },
                new ProductImage { Id = 2, ProductId = 31, ImageUrl = "primary", IsMain = true });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/v1/inventory/parts-page?search=sku-3&page=2&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(10, body.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(35, body.RootElement.GetProperty("items")[0].GetProperty("item").GetProperty("id").GetInt64());
        var first = await client.GetAsync("/api/v1/inventory/parts-page?search=sku-31&page=1&pageSize=5");
        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var item = firstBody.RootElement.GetProperty("items")[0];
        Assert.Equal("Parts", item.GetProperty("categoryName").GetString());
        Assert.Equal("primary", item.GetProperty("imageUrl").GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/inventory/parts-page")).StatusCode);
    }
}
