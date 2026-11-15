using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceRelatedBranchScopeTests
{
    [Fact]
    public async Task DetailAndTaxEndpoints_EnforceBranchScopeBeforeReadingOrWriting()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.RetailOrders.AddRange(
                new RetailOrder { Id = 801, BranchId = 1, OrderCode = "BR-1" },
                new RetailOrder { Id = 802, BranchId = 2, OrderCode = "BR-2" });
            db.Invoices.AddRange(
                new Invoice { Id = 811, RetailOrderId = 801, PayerType = "CUSTOMER", TaxAmount = 10 },
                new Invoice { Id = 812, RetailOrderId = 802, PayerType = "CUSTOMER", TaxAmount = 20 });
            db.InvoiceDetails.AddRange(
                new InvoiceDetail { Id = 821, InvoiceId = 811, ItemType = "SERVICE", ItemName = "One", Unit = "unit", Quantity = 1, UnitPriceAtTime = 10, SubTotal = 10, FinalAmount = 10 },
                new InvoiceDetail { Id = 822, InvoiceId = 812, ItemType = "SERVICE", ItemName = "Two", Unit = "unit", Quantity = 1, UnitPriceAtTime = 20, SubTotal = 20, FinalAmount = 20 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        using var list = await client.GetAsync("/api/v1/invoice-details");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(821, Assert.Single(listJson.RootElement.EnumerateArray()).GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/invoice-details/822")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/invoices/812/view-detail")).StatusCode);
        using var summary = await client.GetAsync("/api/v1/invoices/tax-summary");
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        using var summaryJson = JsonDocument.Parse(await summary.Content.ReadAsStringAsync());
        Assert.Equal(10m, summaryJson.RootElement.GetProperty("totalTax").GetDecimal());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/invoice-details", new { invoiceId = 812, itemType = "SERVICE", itemName = "Test", quantity = 1, unit = "unit", unitPriceAtTime = 1, subTotal = 1, finalAmount = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/v1/invoice-details/821", new { invoiceId = 812, itemName = "Changed" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/v1/invoice-details/822")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        using var adminSummary = await client.GetAsync("/api/v1/invoices/tax-summary");
        using var adminJson = JsonDocument.Parse(await adminSummary.Content.ReadAsStringAsync());
        Assert.Equal(30m, adminJson.RootElement.GetProperty("totalTax").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/invoices/812/view-detail")).StatusCode);
    }
}
