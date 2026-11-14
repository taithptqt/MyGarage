using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class ODataApiTests
{
    [Fact]
    public async Task InvoicesOData_FiltersBeforeODataPaging_ForBranchAndCustomer()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.RetailOrders.AddRange(
                new RetailOrder { Id = 101, BranchId = 1, CustomerId = 100, OrderCode = "A" },
                new RetailOrder { Id = 102, BranchId = 2, CustomerId = 100, OrderCode = "B" },
                new RetailOrder { Id = 103, BranchId = 1, CustomerId = 101, OrderCode = "C" });
            db.Invoices.AddRange(
                new Invoice { Id = 201, RetailOrderId = 101, PayerType = "CUSTOMER", TotalAmount = 10 },
                new Invoice { Id = 202, RetailOrderId = 102, PayerType = "CUSTOMER", TotalAmount = 20 },
                new Invoice { Id = 203, RetailOrderId = 103, PayerType = "CUSTOMER", TotalAmount = 30 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        async Task<long[]> GetIds(string token)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("/odata/Invoices?$orderby=Id&$top=10&$select=Id");
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rows = json.RootElement.ValueKind == JsonValueKind.Array ? json.RootElement : json.RootElement.GetProperty("value");
            return rows.EnumerateArray().Select(x => x.GetProperty("Id").GetInt64()).ToArray();
        }
        Assert.Equal(new long[] { 201, 203 }, await GetIds(TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1)));
        Assert.Equal(new long[] { 201, 202 }, await GetIds(TestWebApplicationFactory.CreateJwtToken(100, "Customer")));
        Assert.Empty(await GetIds(TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER")));
    }

    [Fact]
    public async Task ServiceOrdersOData_EnforcesBranchIsolation_AndSafeQueryLimits()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleModels.Add(new VehicleModel
            {
                Id = 301,
                BrandId = 1,
                ModelName = "Sedan"
            });
            db.Vehicles.Add(new Vehicle
            {
                Id = 300,
                ModelId = 301,
                LicensePlate = "30A-99999"
            });
            db.ServiceOrders.AddRange(
                new ServiceOrder
                {
                    Id = 1001,
                    Code = "SO-BR1-001",
                    CustomerId = 100,
                    VehicleId = 300,
                    ExecuteBranchId = 1,
                    Status = "CHECKED_IN"
                },
                new ServiceOrder
                {
                    Id = 1002,
                    Code = "SO-BR2-001",
                    CustomerId = 100,
                    VehicleId = 300,
                    ExecuteBranchId = 2,
                    Status = "CHECKED_IN"
                });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var branch1ManagerToken = TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", branch1ManagerToken);

        var okResponse = await client.GetAsync("/odata/ServiceOrders?$top=5&$select=Id,Code,ExecuteBranchId");
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);

        var payload = await okResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        var items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToList()
            : doc.RootElement.GetProperty("value").EnumerateArray().ToList();

        Assert.Single(items);
        Assert.Equal(1001L, items[0].GetProperty("Id").GetInt64());
        Assert.Equal("SO-BR1-001", items[0].GetProperty("Code").GetString());

        var excessiveTop = await client.GetAsync("/odata/ServiceOrders?$top=101");
        Assert.Equal(HttpStatusCode.BadRequest, excessiveTop.StatusCode);

        var expandBlocked = await client.GetAsync("/odata/ServiceOrders?$expand=Customer");
        Assert.Equal(HttpStatusCode.BadRequest, expandBlocked.StatusCode);

        var unfilterableField = await client.GetAsync("/odata/ServiceOrders?$filter=CustomerName eq 'Test'");
        Assert.Equal(HttpStatusCode.BadRequest, unfilterableField.StatusCode);
    }
}
