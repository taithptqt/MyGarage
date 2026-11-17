using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class RetailOrderPagingTests
{
    [Fact]
    public async Task CustomerPage_UsesScopedTotalsAndFindsAnchorAcrossPages()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 45; i++)
                db.RetailOrders.Add(new RetailOrder { Id = i, BranchId = 1, CustomerId = 100,
                    OrderCode = $"RO{i}", CreatedAt = new DateTime(2026, 1, 1).AddMinutes(i),
                    Status = i % 2 == 0 ? "CONFIRMED" : "DRAFT", FinalAmount = 100m });
            db.RetailOrders.Add(new RetailOrder { Id = 100, BranchId = 1, CustomerId = 999,
                OrderCode = "OTHER", Status = "CONFIRMED", FinalAmount = 999m });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        var response = await client.GetAsync("/api/v1/retail-orders/customers/current/page?page=1&pageSize=20&anchorId=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var canonicalPage = await client.GetAsync("/api/v1/customers/current/retail-orders/page?page=1&pageSize=20&anchorId=1");
        Assert.Equal(response.StatusCode, canonicalPage.StatusCode);
        Assert.Equal(await response.Content.ReadAsStringAsync(), await canonicalPage.Content.ReadAsStringAsync());
        using var legacyList = await client.GetAsync("/api/v1/retail-orders/customers/current?page=1&pageSize=10");
        using var canonicalList = await client.GetAsync("/api/v1/customers/current/retail-orders?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, canonicalList.StatusCode);
        Assert.Equal(legacyList.StatusCode, canonicalList.StatusCode);
        Assert.Equal(await legacyList.Content.ReadAsStringAsync(), await canonicalList.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;
        Assert.Equal(45, body.GetProperty("total").GetInt32());
        Assert.Equal(23, body.GetProperty("draftCount").GetInt32());
        Assert.Equal(22, body.GetProperty("confirmedCount").GetInt32());
        Assert.Equal(4500m, body.GetProperty("totalValue").GetDecimal());
        Assert.Equal(3, body.GetProperty("page").GetInt32());
        Assert.Equal(5, body.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("items")[4].GetProperty("id").GetInt64());

        var foreignAnchor = await client.GetAsync("/api/v1/retail-orders/customers/current/page?page=1&pageSize=20&anchorId=100");
        using var foreignJson = JsonDocument.Parse(await foreignAnchor.Content.ReadAsStringAsync());
        Assert.Equal(1, foreignJson.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(45, foreignJson.RootElement.GetProperty("total").GetInt32());

        var lastPage = await client.GetAsync("/api/v1/retail-orders/customers/current/page?page=2147483647&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, lastPage.StatusCode);
        using var lastPageJson = JsonDocument.Parse(await lastPage.Content.ReadAsStringAsync());
        Assert.Equal(3, lastPageJson.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(5, lastPageJson.RootElement.GetProperty("items").GetArrayLength());

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/retail-orders/100")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/retail-orders/customers/current/page")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/customers/current/retail-orders/page")).StatusCode);
    }
}
