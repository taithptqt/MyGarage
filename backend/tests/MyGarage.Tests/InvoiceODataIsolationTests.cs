using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceODataIsolationTests
{
    private static async Task<TestWebApplicationFactory> CreateSeededFactory()
    {
        var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Staffs.Add(new Staff { Id = 202, FullName = "Manager branch 2", Phone = "0902223345",
                Email = "branch2@example.com", PasswordHash = "hash", BranchId = 2, Status = "ACTIVE", SecurityStamp = "test-stamp" });
            db.Customers.Add(new Customer { Id = 101, FullName = "Customer B", Phone = "0901112234",
                Email = "customer101@example.com", PasswordHash = "hash", Type = "INDIVIDUAL", SecurityStamp = "test-stamp", IsActive = true });
            db.RetailOrders.AddRange(
                new RetailOrder { Id = 101, BranchId = 1, CustomerId = 100, OrderCode = "A" },
                new RetailOrder { Id = 102, BranchId = 2, CustomerId = 100, OrderCode = "B" },
                new RetailOrder { Id = 103, BranchId = 1, CustomerId = 101, OrderCode = "C" },
                new RetailOrder { Id = 104, BranchId = 1, CustomerId = 100, OrderCode = "D" });
            db.Invoices.AddRange(
                new Invoice { Id = 201, RetailOrderId = 101, PayerType = "CUSTOMER", TotalAmount = 10 },
                new Invoice { Id = 202, RetailOrderId = 102, PayerType = "CUSTOMER", TotalAmount = 20 },
                new Invoice { Id = 203, RetailOrderId = 103, PayerType = "CUSTOMER", TotalAmount = 30 },
                new Invoice { Id = 204, RetailOrderId = 104, PayerType = "CUSTOMER", TotalAmount = 40 });
            return Task.CompletedTask;
        });
        return factory;
    }

    [Fact]
    public async Task Invoices_EnforcesAnonymousAdminBranchAndCustomerScope()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/odata/Invoices")).StatusCode);
        async Task<long[]> ReadIds(string token)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("/odata/Invoices?$orderby=Id&$select=Id");
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rows = json.RootElement.ValueKind == JsonValueKind.Array ? json.RootElement : json.RootElement.GetProperty("value");
            return rows.EnumerateArray().Select(x => x.GetProperty("Id").GetInt64()).ToArray();
        }
        Assert.Equal(new long[] { 201, 202, 203, 204 }, await ReadIds(TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1)));
        Assert.Equal(new long[] { 202 }, await ReadIds(TestWebApplicationFactory.CreateJwtToken(202, "Staff", "MANAGER", 2)));
        Assert.Equal(new long[] { 203 }, await ReadIds(TestWebApplicationFactory.CreateJwtToken(101, "Customer")));
    }

    [Fact]
    public async Task Invoices_AppliesFilterCountAndPaginationWithinAuthorizedScope_AndRejectsInvalidQueries()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/odata/Invoices?$filter=TotalAmount%20ge%2020&$orderby=Id&$count=true&$skip=1&$top=1&$select=Id");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("@odata.count").GetInt64());
        var row = Assert.Single(json.RootElement.GetProperty("value").EnumerateArray());
        Assert.Equal(204, row.GetProperty("Id").GetInt64());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/odata/Invoices?$top=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/odata/Invoices?$expand=RetailOrder")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/odata/Invoices?$filter=UnknownField%20eq%201")).StatusCode);
    }
}
