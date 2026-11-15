using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataInvoiceMixedSourceScopeTests
{
    [Theory]
    [InlineData("/odata")]
    [InlineData("/api/v1/odata")]
    public async Task MixedSources_StaffScopeUsesServiceOrderBranch_WhileCustomerOwnershipRemainsCompatible(string prefix)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Staffs.Add(new Staff { Id = 202, FullName = "Branch 2", Phone = "0901234202", Email = "staff202@example.com", PasswordHash = "hash", BranchId = 2, Status = "ACTIVE", SecurityStamp = "test-stamp" });
            db.Customers.Add(new Customer { Id = 101, FullName = "Customer 2", Phone = "0901234101", Email = "customer101@example.com", PasswordHash = "hash", Type = "INDIVIDUAL", IsActive = true, SecurityStamp = "test-stamp" });
            db.ServiceOrders.Add(new ServiceOrder { Id = 501, Code = "SO-MIXED", CustomerId = 100, VehicleId = 1, ExecuteBranchId = 1, Status = "CHECKED_IN" });
            db.RetailOrders.Add(new RetailOrder { Id = 502, BranchId = 2, CustomerId = 101, OrderCode = "RO-MIXED" });
            db.Invoices.AddRange(
                new Invoice { Id = 503, ServiceOrderId = 501, RetailOrderId = 502, PayerType = "CUSTOMER", TotalAmount = 10 },
                new Invoice { Id = 504, ServiceOrderId = 999999, RetailOrderId = 502, PayerType = "CUSTOMER", TotalAmount = 20 });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        async Task<long[]> ReadIds(string token)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.GetAsync($"{prefix}/Invoices?$orderby=Id&$select=Id&$count=true");
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);
            using var json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("value").EnumerateArray().Select(row => row.GetProperty("Id").GetInt64()).ToArray();
        }

        Assert.Equal(new long[] { 503 }, await ReadIds(TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1)));
        Assert.Empty(await ReadIds(TestWebApplicationFactory.CreateJwtToken(202, "Staff", "MANAGER", 2)));
        Assert.Equal(new long[] { 503, 504 }, await ReadIds(TestWebApplicationFactory.CreateJwtToken(101, "Customer")));
        Assert.Equal(new long[] { 503 }, await ReadIds(TestWebApplicationFactory.CreateJwtToken(100, "Customer")));
        Assert.Equal(new long[] { 503, 504 }, await ReadIds(TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1)));
    }
}
