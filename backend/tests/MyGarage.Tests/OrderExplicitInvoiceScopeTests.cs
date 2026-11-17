using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class OrderExplicitInvoiceScopeTests
{
    [Theory]
    [InlineData(false, "CUSTOMER", 100L, 701L, 200)]
    [InlineData(true, "CUSTOMER", 100L, 701L, 200)]
    [InlineData(false, "CUSTOMER", 100L, 702L, 400)]
    [InlineData(true, "CUSTOMER", 100L, 702L, 400)]
    [InlineData(true, "INSURANCE", 300L, 702L, 200)]
    [InlineData(true, "CUSTOMER", 100L, 703L, 400)]
    [InlineData(true, "INSURANCE", 300L, 701L, 400)]
    [InlineData(true, "CUSTOMER", null, 701L, 400)]
    [InlineData(true, null, 100L, 701L, 400)]
    public async Task ExplicitInvoice_MustMatchSourceAndPayer(bool staff, string? payerType, long? payerId, long invoiceId, int expected)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceOrders.Add(new ServiceOrder { Id = 700, CustomerId = 100, VehicleId = 1, Code = "SO-700", OriginBranchId = 1, ExecuteBranchId = 1, Status = "IN_PROGRESS" });
            db.Invoices.AddRange(
                new Invoice { Id = 701, ServiceOrderId = 700, PayerType = "CUSTOMER", PayerId = 100, TotalAmount = 10000 },
                new Invoice { Id = 702, ServiceOrderId = 700, PayerType = "INSURANCE", PayerId = 300, TotalAmount = 10000 },
                new Invoice { Id = 703, ServiceOrderId = 700, PayerType = "CUSTOMER", PayerId = null, TotalAmount = 10000 });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<IOrderCommandFacade>();
        var result = await facade.PrepareCreatePaymentAsync(new OrderCreateRequest
        {
            ServiceOrderId = 700, InvoiceId = invoiceId, PayerType = payerType, PayerId = payerId
        }, 100, staff);
        Assert.Equal(expected, result.StatusCode);
        if (expected == 200) Assert.Equal(invoiceId, result.Data!.ResolvedInvoiceId);
    }

    [Fact]
    public async Task ExplicitInvoice_WithDifferentSource_IsRejected()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Invoices.Add(new Invoice { Id = 701, ServiceOrderId = 700, PayerType = "CUSTOMER", PayerId = 100, TotalAmount = 10000 });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<IOrderCommandFacade>();
        var result = await facade.PrepareCreatePaymentAsync(new OrderCreateRequest
        {
            ServiceOrderId = 999, InvoiceId = 701, PayerType = "CUSTOMER", PayerId = 100
        }, 100, true);
        Assert.Equal(400, result.StatusCode);
    }
    [Fact]
    public async Task MultipleMatchingInvoices_ReturnsBadRequestBeforeCreatingPaymentLink()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceOrders.Add(new ServiceOrder { Id = 700, CustomerId = 100, VehicleId = 1, Code = "SO-700", OriginBranchId = 1, ExecuteBranchId = 1, Status = "IN_PROGRESS" });
            db.Invoices.AddRange(
                new Invoice { Id = 701, ServiceOrderId = 700, PayerType = "CUSTOMER", PayerId = 100, TotalAmount = 40 },
                new Invoice { Id = 702, ServiceOrderId = 700, PayerType = "CUSTOMER", PayerId = 100, TotalAmount = 60 });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<IOrderCommandFacade>();
        var result = await facade.PrepareCreatePaymentAsync(new OrderCreateRequest { ServiceOrderId = 700 }, 100, false);
        Assert.Equal(400, result.StatusCode);
        Assert.Null(result.Data);
    }
}
