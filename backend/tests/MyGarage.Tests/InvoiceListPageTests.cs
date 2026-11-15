using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceListPageTests
{
    [Fact]
    public async Task ListPage_ScopesStaffAndPreservesAdminFiltersSummaryAndOrder()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.RetailOrders.AddRange(new RetailOrder { Id = 1, BranchId = 1, OrderCode = "B1" },
                new RetailOrder { Id = 2, BranchId = 2, OrderCode = "B2" });
            db.Invoices.AddRange(
                new Invoice { Id = 1, RetailOrderId = 1, PayerType = "CUSTOMER", PaymentStatus = "paid", CompanyName = "Công ty A", ReceiveEmail = "a@example.com", IssuedAt = new DateTime(2026, 1, 1) },
                new Invoice { Id = 2, RetailOrderId = 2, PayerType = "CUSTOMER", PaymentStatus = null, CompanyName = "Company B" },
                new Invoice { Id = 3, RetailOrderId = 2, PayerType = "CUSTOMER", PaymentStatus = "UNKNOWN", CompanyName = "Company C" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/invoices/list-page")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/invoices/list-page")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var scoped = await client.GetAsync("/api/v1/invoices/list-page?source=RETAIL&status=UNKNOWN");
        Assert.Equal(HttpStatusCode.OK, scoped.StatusCode);
        using var scopedJson = JsonDocument.Parse(await scoped.Content.ReadAsStringAsync());
        Assert.Equal(0, scopedJson.RootElement.GetProperty("total").GetInt32());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var response = await client.GetAsync("/api/v1/invoices/list-page?source=RETAIL&status=UNKNOWN&page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("summary").GetProperty("unknownStatusCount").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        var search = await client.GetAsync("/api/v1/invoices/list-page?search=C%C3%94NG%20TY&status=PAID");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using var searchJson = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
        Assert.Equal(1, searchJson.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, searchJson.RootElement.GetProperty("summary").GetProperty("paidCount").GetInt32());
        Assert.Equal("a@example.com", searchJson.RootElement.GetProperty("items")[0].GetProperty("receiveEmail").GetString());
        var noAccent = await client.GetAsync("/api/v1/invoices/list-page?search=cong%20ty");
        using var noAccentJson = JsonDocument.Parse(await noAccent.Content.ReadAsStringAsync());
        Assert.Equal(0, noAccentJson.RootElement.GetProperty("total").GetInt32());
    }
}
