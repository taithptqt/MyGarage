using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceRestBranchScopeTests
{
    [Fact]
    public async Task StaffScope_AppliesBeforePageAndDetail_AndRejectsCrossBranchWrites()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.RetailOrders.AddRange(
                new RetailOrder { Id = 8101, BranchId = 1, CustomerId = 100, OrderCode = "BR1" },
                new RetailOrder { Id = 8102, BranchId = 2, CustomerId = 100, OrderCode = "BR2" });
            db.Invoices.AddRange(
                new Invoice { Id = 8201, RetailOrderId = 8101, PayerType = "CUSTOMER", TotalAmount = 10 },
                new Invoice { Id = 8202, RetailOrderId = 8102, PayerType = "CUSTOMER", TotalAmount = 20 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        using var list = await client.GetAsync("/api/v1/invoices?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(8201, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/invoices/8202")).StatusCode);
        using var page = await client.GetAsync("/api/v1/invoices/list-page?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var pageJson = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        Assert.Equal(1, pageJson.RootElement.GetProperty("total").GetInt32());
        using var create = await client.PostAsJsonAsync("/api/v1/invoices", new { retailOrderId = 8102 });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/invoices/completed-payment-backfills", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/invoices/8202")).StatusCode);
    }
}
