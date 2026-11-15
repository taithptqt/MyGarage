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

public sealed class InvoiceViewDetailTests
{
    [Fact]
    public async Task ViewDetail_RequiresStaffScopesBranchAndMatchesAdminShape()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.RetailOrders.Add(new RetailOrder { Id = 10, BranchId = 2, OrderCode = "OTHER-BRANCH" });
            db.Invoices.AddRange(
                new Invoice { Id = 30, RetailOrderId = 10, PayerType = "CUSTOMER", CompanyName = "Company", ReceiveEmail = "test@example.com", TotalAmount = 100, PaidAmount = 40 },
                new Invoice { Id = 31, PayerType = "CUSTOMER" });
            db.InvoiceDetails.AddRange(
                new InvoiceDetail { Id = 40, InvoiceId = 30, ItemType = "PART", ItemName = "A", Unit = "unit", Quantity = 1, FinalAmount = 10 },
                new InvoiceDetail { Id = 41, InvoiceId = 30, ItemType = "PART", ItemName = "B", Unit = "unit", Quantity = 1, FinalAmount = 20 },
                new InvoiceDetail { Id = 42, InvoiceId = 31, ItemType = "PART", ItemName = "Other", Unit = "unit", Quantity = 1 });
            db.Payments.AddRange(
                new Payment { Id = 50, InvoiceId = 30, Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 10 },
                new Payment { Id = 51, InvoiceId = 30, Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 20 },
                new Payment { Id = 52, InvoiceId = 31, Type = "PAYMENT", Direction = "INFLOW", MethodId = 1, Amount = 30 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/invoices/30/view-detail")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/invoices/30/view-detail")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/invoices/30/view-detail")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/invoices/999/view-detail")).StatusCode);
        var result = await client.GetAsync("/api/v1/invoices/30/view-detail?detailsPage=2&paymentsPage=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var payload = JsonNode.Parse(await result.Content.ReadAsStringAsync())!;
        Assert.Equal(2, (int)payload["detailsTotal"]!);
        Assert.Equal(2, (int)payload["paymentsTotal"]!);
        Assert.Equal(2, (int)payload["detailsPage"]!);
        Assert.Equal(1, (int)payload["paymentsPage"]!);
        Assert.Equal(1, (int)payload["pageSize"]!);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(mapper.Map<InvoiceResponseDto>(db.Invoices.Single(x => x.Id == 30)), jsonOptions), payload["invoice"]));
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(mapper.Map<InvoiceDetailResponseDto>(db.InvoiceDetails.Single(x => x.Id == 41)), jsonOptions), payload["details"]![0]));
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(mapper.Map<PaymentResponseDto>(db.Payments.Single(x => x.Id == 50)), jsonOptions), payload["payments"]![0]));
        var outOfRange = await client.GetAsync("/api/v1/invoices/30/view-detail?detailsPage=2147483647&paymentsPage=2147483647&pageSize=1000");
        var empty = JsonNode.Parse(await outOfRange.Content.ReadAsStringAsync())!;
        Assert.Empty(empty["details"]!.AsArray());
        Assert.Empty(empty["payments"]!.AsArray());
        Assert.Equal(100, (int)empty["pageSize"]!);
    }
}
