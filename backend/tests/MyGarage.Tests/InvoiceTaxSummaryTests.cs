using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceTaxSummaryTests
{
    [Fact]
    public async Task Summary_MatchesLegacyStaffScopeTotalsAndTopThirty()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.RetailOrders.AddRange(
                new RetailOrder { Id = 1, BranchId = 1, OrderCode = "B1" },
                new RetailOrder { Id = 2, BranchId = 2, OrderCode = "B2" });
            for (var id = 1; id <= 32; id++)
                db.Invoices.Add(new Invoice { Id = id, RetailOrderId = id % 2 == 0 ? 1 : 2, PayerType = "CUSTOMER", TaxAmount = id == 1 ? -2m : 1m });
            db.Invoices.AddRange(
                new Invoice { Id = 33, PayerType = "CUSTOMER", TaxAmount = null },
                new Invoice { Id = 34, PayerType = "CUSTOMER", TaxAmount = 0m });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/invoices/tax-summary")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/invoices/tax-summary")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/invoices/tax-summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(29m, json.RootElement.GetProperty("totalTax").GetDecimal());
        Assert.Equal(32, json.RootElement.GetProperty("taxedCount").GetInt32());
        var rows = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(30, rows.Length);
        Assert.Equal(32, rows[0].GetProperty("id").GetInt64());
        Assert.Equal(3, rows[29].GetProperty("id").GetInt64());
        Assert.Equal(1, rows[0].GetProperty("retailOrderId").GetInt64());
        Assert.Equal(2, rows[1].GetProperty("retailOrderId").GetInt64());
        Assert.False(rows[0].TryGetProperty("receiveEmail", out _));
    }
}
