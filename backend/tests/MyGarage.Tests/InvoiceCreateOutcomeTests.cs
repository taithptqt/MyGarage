using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceCreateOutcomeTests
{
    [Fact]
    public async Task Create_ReturnsCreatedOnlyWhenNewInvoiceIsInserted()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.RetailOrders.AddRange(
                new RetailOrder { Id = 6101, BranchId = 1, CustomerId = 100, OrderCode = "PREEXISTING" },
                new RetailOrder { Id = 6102, BranchId = 1, CustomerId = 100, OrderCode = "NEW" });
            db.Invoices.Add(new Invoice { Id = 6201, RetailOrderId = 6101, PayerType = "CUSTOMER", PayerId = 100 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        using var existing = await client.PostAsJsonAsync("/api/v1/invoices", new { retailOrderId = 6101 });
        Assert.True(existing.StatusCode == HttpStatusCode.OK, await existing.Content.ReadAsStringAsync());
        using var existingJson = JsonDocument.Parse(await existing.Content.ReadAsStringAsync());
        Assert.Equal(6201, existingJson.RootElement.GetProperty("id").GetInt64());
        using var created = await client.PostAsJsonAsync("/api/v1/invoices", new { retailOrderId = 6102 });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        Assert.NotNull(created.Headers.Location);
    }
}
