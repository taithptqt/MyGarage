using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class CustomerPaymentsQueryTests
{
    [Fact]
    public async Task Status_RejectsStaffIdCollisionAndAllowsOwningCustomerOnly()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Payments.AddRange(
                new Payment { Id = 1, PayerId = 200, PayerType = "CUSTOMER", Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 11, Status = "PENDING" },
                new Payment { Id = 2, PayerId = 100, PayerType = "CUSTOMER", Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 22, Status = "COMPLETED" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/payments/2/status")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/payments/1/status")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/payments/1/status")).StatusCode);
        var response = await client.GetAsync("/api/payments/2/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("id").GetInt64());
        Assert.Equal("COMPLETED", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Mine_RejectsStaffWithSameNumericIdAsCustomerPayer()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Payments.Add(new Payment { Id = 1, PayerId = 200, PayerType = "CUSTOMER", Type = "PAYMENT",
                Direction = "INFLOW", MethodId = 1, Amount = 11 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/payments/mine");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Mine_ResponseMatchesLegacyPaymentDtoMapping()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Payments.Add(new Payment { Id = 5, PayerId = 100, PayerType = "CUSTOMER", Type = "PAYMENT",
                Direction = "INFLOW", MethodId = 1, Amount = 99.50m, TransactionCode = "TX", Status = "COMPLETED",
                ShiftClosingId = 11, CashierId = 12, ServiceOrderId = 13, InvoiceId = 14, RetailOrderId = 15,
                ReversedPaymentId = 16, CreatedAt = new DateTime(2026, 1, 1), UpdatedAt = new DateTime(2026, 1, 2), UpdatedBy = 200 });
            return Task.CompletedTask;
        });
        JsonNode? expected;
        using (var scope = factory.Services.CreateScope())
        {
            var payment = scope.ServiceProvider.GetRequiredService<GarageContext>().Payments.Single(x => x.Id == 5);
            var dto = scope.ServiceProvider.GetRequiredService<IMapper>().Map<PaymentResponseDto>(payment);
            expected = JsonSerializer.SerializeToNode(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        var response = await client.GetAsync("/api/payments/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["items"]!.AsArray();
        Assert.Single(items);
        Assert.True(JsonNode.DeepEquals(expected, items[0]), $"Expected {expected}; got {items[0]}");
    }

    [Fact]
    public async Task Mine_RequiresAuthenticationAndIsolatesDifferentCustomers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Customers.Add(new Customer { Id = 101, FullName = "Customer B", Phone = "0901112234",
                Email = "customer101@example.com", PasswordHash = "hash", Type = "INDIVIDUAL", SecurityStamp = "test-stamp", IsActive = true });
            db.Payments.AddRange(
                new Payment { Id = 1, PayerId = 100, PayerType = "CUSTOMER", Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 11 },
                new Payment { Id = 2, PayerId = 101, PayerType = "CUSTOMER", Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 22 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/payments/mine")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(101, "Customer"));
        var response = await client.GetAsync("/api/payments/mine?page=0&pageSize=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Total-Count"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(50, json.RootElement.GetProperty("pageSize").GetInt32());
        var row = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(2, row.GetProperty("id").GetInt64());
        var capped = await client.GetAsync("/api/payments/mine?pageSize=500");
        using var cappedJson = JsonDocument.Parse(await capped.Content.ReadAsStringAsync());
        Assert.Equal(100, cappedJson.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Mine_PreservesScopeOrderPaginationHeadersAndShape()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            var date = new DateTime(2026, 1, 1);
            db.Payments.AddRange(
                new Payment { Id = 1, PayerId = 100, PayerType = "CUSTOMER", Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 11, CreatedAt = date },
                new Payment { Id = 2, PayerId = 100, PayerType = "customer", Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 22, CreatedAt = date },
                new Payment { Id = 3, PayerId = 101, PayerType = "CUSTOMER", Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 33, CreatedAt = date },
                new Payment { Id = 4, PayerId = 100, PayerType = "STAFF", Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 44, CreatedAt = date });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        var response = await client.GetAsync("/api/payments/mine?page=2&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Total-Count"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("totalPages").GetInt32());
        var row = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(1, row.GetProperty("id").GetInt64());
        Assert.Equal(11, row.GetProperty("amount").GetDecimal());
        Assert.True(row.TryGetProperty("updatedBy", out _));
        Assert.False(row.TryGetProperty("direction", out _));
        var beyond = await client.GetAsync("/api/payments/mine?page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, beyond.StatusCode);
        using var beyondJson = JsonDocument.Parse(await beyond.Content.ReadAsStringAsync());
        Assert.Empty(beyondJson.RootElement.GetProperty("items").EnumerateArray());
    }
}
