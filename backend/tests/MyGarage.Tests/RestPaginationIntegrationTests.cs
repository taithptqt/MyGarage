using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class RestPaginationIntegrationTests
{
    [Fact]
    public async Task RetailCatalog_UsesFilteredPagesAndPreferredStock()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Categories.Add(new Category { Id = 10, CategoryName = "Brakes" });
            db.Warehouses.AddRange(
                new Warehouse { Id = 1, BranchId = 1, Code = "A", Name = "Main", Type = "GENERAL" },
                new Warehouse { Id = 2, BranchId = 1, Code = "B", Name = "Retail", Type = "RETAIL" });
            for (var i = 1; i <= 15; i++)
                db.Products.Add(new Product { Id = i, Name = $"Part {i:D2}", Sku = $"SKU{i}", Unit = "unit", CategoryId = 10, IsActive = true });
            db.ProductImages.Add(new ProductImage { Id = 1, ProductId = 15, ImageUrl = "/a.png", Description = "special match" });
            db.Inventories.AddRange(
                new Inventory { Id = 1, ProductId = 15, WarehouseId = 1, Quantity = 5 },
                new Inventory { Id = 2, ProductId = 15, WarehouseId = 2, Quantity = 0 },
                new Inventory { Id = 3, ProductId = 14, WarehouseId = 1, Quantity = 2 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        var page = await client.GetAsync("/api/products/retail-catalog?branchId=1&page=1&pageSize=12&sort=name-asc");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var document = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        Assert.Equal(15, document.RootElement.GetProperty("total").GetInt32());
        var items = document.RootElement.GetProperty("items");
        Assert.Equal(12, items.GetArrayLength());
        Assert.Equal(14, items[0].GetProperty("id").GetInt64());
        Assert.False(items[0].TryGetProperty("costPrice", out _));
        var secondPage = await client.GetAsync("/api/products/retail-catalog?branchId=1&page=2&pageSize=12&sort=name-asc");
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        using var secondJson = JsonDocument.Parse(await secondPage.Content.ReadAsStringAsync());
        Assert.Equal(3, secondJson.RootElement.GetProperty("items").GetArrayLength());
        var firstIds = items.EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToHashSet();
        Assert.DoesNotContain(secondJson.RootElement.GetProperty("items").EnumerateArray(), x => firstIds.Contains(x.GetProperty("id").GetInt64()));
        var filtered = await client.GetAsync("/api/products/retail-catalog?search=special&categoryId=10&page=1&pageSize=12");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        using var filteredJson = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        Assert.Equal(15, filteredJson.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(0, filteredJson.RootElement.GetProperty("availability").GetProperty("15").GetInt32());
    }

    [Fact]
    public async Task CustomerNotifications_PageAndUnreadFilterRemainCustomerScoped()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 60; i++)
                db.Notifications.Add(new Notification { Id = i, CustomerId = i <= 55 ? 100 : 999,
                    Title = "Test", Content = "Test", IsRead = i % 2 == 0, SentAt = new DateTime(2026, 1, 1) });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        var result = await client.GetAsync("/api/customers/me/notifications?unreadOnly=true&page=2&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        Assert.Equal(28, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(28, json.RootElement.GetProperty("unreadCount").GetInt32());
        var items = json.RootElement.GetProperty("items");
        Assert.Equal(10, items.GetArrayLength());
        Assert.Equal(35, items[0].GetProperty("id").GetInt64());
        var outOfRange = await client.GetAsync("/api/customers/me/notifications?page=2147483647&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, outOfRange.StatusCode);
        using var empty = JsonDocument.Parse(await outOfRange.Content.ReadAsStringAsync());
        Assert.Equal(55, empty.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, empty.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ApprovalRequests_GenericPagingReturnsStableSecondPage()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 65; i++)
                db.ApprovalRequests.Add(new ApprovalRequest { Id = i, RequestType = "TEST", RequestedBy = 200 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var result = await client.GetAsync("/api/approval-requests?page=2&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.False(result.Headers.Contains("X-Total-Count"));
        using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        Assert.Equal(65, json.RootElement.GetProperty("totalCount").GetInt32());
        var items = json.RootElement.GetProperty("items");
        Assert.Equal(15, items.GetArrayLength());
        Assert.Equal(51, items[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Appointments_PaginationPreservesBranchScopeAndStatus()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 65; i++)
                db.Appointments.Add(new Appointment {
                    Id = i, BranchId = i <= 60 ? 1 : 2, CustomerId = 100,
                    Status = "PENDING", StartTime = new DateTime(2026, 1, 1).AddMinutes(i),
                    EndTime = new DateTime(2026, 1, 1).AddMinutes(i + 30)
                });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var result = await client.GetAsync("/api/appointments?status=PENDING&page=2&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.False(result.Headers.Contains("X-Total-Count"));
        using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        Assert.Equal(60, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(10, json.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Products_DefaultAndExplicitPagesUseSameOrderingAndLimit()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            for (var i = 1; i <= 101; i++)
                db.Products.Add(new Product { Id = i, Sku = $"SKU{i}", Name = $"Product{i}", Unit = "unit", IsDeleted = false });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        var first = await client.GetAsync("/api/products");
        var second = await client.GetAsync("/api/products?page=2&pageSize=50");
        var limited = await client.GetAsync("/api/products?page=1&pageSize=500");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.False(limited.Headers.Contains("X-Page-Size"));
        using var limitedJson = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        Assert.Equal(100, limitedJson.RootElement.GetProperty("pageSize").GetInt32());
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(101, firstJson.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(51, secondJson.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task VehiclesEndpoint_ReturnsRawJsonArray_WithPaginationHeaders()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.VehicleBrands.Add(new VehicleBrand { Id = 10, Code = "TOY", Name = "Toyota", IsActive = true });
            db.VehicleModels.Add(new VehicleModel { Id = 20, BrandId = 10, ModelName = "Camry" });

            for (int i = 1; i <= 25; i++)
            {
                db.Vehicles.Add(new Vehicle
                {
                    Id = 500 + i,
                    ModelId = 20,
                    LicensePlate = $"30A-{10000 + i}",
                    Color = "White"
                });
            }

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var staffToken = TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        // Act: GET page 1 with pageSize 10
        var response = await client.GetAsync("/api/vehicles?page=1&pageSize=10");

        // Assert HTTP Status & No X-Total-Count Headers
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Total-Count"));
        Assert.False(response.Headers.Contains("X-Page"));
        Assert.False(response.Headers.Contains("X-Page-Size"));
        Assert.False(response.Headers.Contains("X-Total-Pages"));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal(25, doc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(10, doc.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, doc.RootElement.GetProperty("totalPages").GetInt32());
        Assert.False(doc.RootElement.GetProperty("hasPreviousPage").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(10, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task VehiclesEndpoint_PageTwo_ReturnsSecondSlice()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.VehicleBrands.Add(new VehicleBrand { Id = 11, Code = "HON", Name = "Honda", IsActive = true });
            db.VehicleModels.Add(new VehicleModel { Id = 21, BrandId = 11, ModelName = "Civic" });

            for (int i = 1; i <= 25; i++)
            {
                db.Vehicles.Add(new Vehicle
                {
                    Id = 600 + i,
                    ModelId = 21,
                    LicensePlate = $"29B-{20000 + i}",
                    Color = "Black"
                });
            }

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var staffToken = TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        // Act: GET page 3 with pageSize 10 (25 items => page 3 has 5 items)
        var response = await client.GetAsync("/api/vehicles?page=3&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Total-Count"));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal(3, doc.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(25, doc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(5, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task VehiclesEndpoint_UnboundedQuery_AppliesDefaultPaginationLimits()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.VehicleBrands.Add(new VehicleBrand { Id = 12, Code = "FOR", Name = "Ford", IsActive = true });
            db.VehicleModels.Add(new VehicleModel { Id = 22, BrandId = 12, ModelName = "Ranger" });

            for (int i = 1; i <= 60; i++)
            {
                db.Vehicles.Add(new Vehicle
                {
                    Id = 700 + i,
                    ModelId = 22,
                    LicensePlate = $"51C-{30000 + i}",
                    Color = "Silver"
                });
            }

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var staffToken = TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        // Request without page/pageSize query params -> should default to page=1, pageSize=50
        var response = await client.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Total-Count"));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(50, doc.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(60, doc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("totalPages").GetInt32());
        // Page 1 of 50 items
        Assert.Equal(50, doc.RootElement.GetProperty("items").GetArrayLength());
    }
}
