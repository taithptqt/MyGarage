using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceAutomaticPayerSelectionTests
{
    private static Task SeedOrder(GarageContext db)
    {
        TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
        db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "B1", Name = "Brand" });
        db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "Model" });
        db.Vehicles.Add(new Vehicle { Id = 1, ModelId = 1, LicensePlate = "TEST-1" });
        db.ServiceOrders.Add(new ServiceOrder { Id = 900, Code = "SO-900", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "IN_PROGRESS", FinalAmount = 100 });
        db.ServiceOrderDetails.Add(new ServiceOrderDetail { Id = 901, ServiceOrderId = 900, ItemType = "LABOR", ItemName = "Work", Unit = "job", PayerType = "CUSTOMER", ServiceSource = "INITIAL", Status = "APPROVED", Quantity = 1, SubTotal = 100, FinalAmount = 100 });
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExistingInvoice_IsSelectedByPayerRatherThanLatest_WithoutModifyingIt()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(async db => { await SeedOrder(db); db.Invoices.AddRange(
            new Invoice { Id = 910, ServiceOrderId = 900, PayerType = "CUSTOMER", PayerId = 100, TotalAmount = 40 },
            new Invoice { Id = 911, ServiceOrderId = 900, PayerType = "INSURANCE", PayerId = 300, TotalAmount = 60 }); });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        var invoice = await service.EnsureForPaymentContextAsync(900, null, "CUSTOMER", 100);
        Assert.NotNull(invoice);
        Assert.Equal(910, invoice.Id);
        Assert.Equal(40, invoice.TotalAmount);
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Empty(await db.InvoiceDetails.ToListAsync());
        Assert.Equal(40, (await db.Invoices.SingleAsync(x => x.Id == 910)).TotalAmount);
    }

    [Fact]
    public async Task MultipleInvoicesForSamePayer_RequireExplicitSelection()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(async db => { await SeedOrder(db); db.Invoices.AddRange(
            new Invoice { Id = 910, ServiceOrderId = 900, PayerType = "CUSTOMER", PayerId = 100, TotalAmount = 40 },
            new Invoice { Id = 911, ServiceOrderId = 900, PayerType = "CUSTOMER", PayerId = 100, TotalAmount = 60 }); });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureForPaymentContextAsync(900, null, "CUSTOMER", 100));
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<GarageContext>().Invoices.CountAsync());
    }

    [Fact]
    public async Task SplitInsuranceOrder_DoesNotAutomaticallyCreateFullOrderInvoice()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(async db => { await SeedOrder(db); var detail = db.ServiceOrderDetails.Local.Single(); detail.PayerType = "SPLIT"; detail.InsuranceCompanyId = 300; detail.InsuranceApprovedAmount = 60; detail.CustomerCoPayAmount = 40; });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureForPaymentContextAsync(900, null, "CUSTOMER", 100));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<GarageContext>().Invoices.ToListAsync());
    }

    [Fact]
    public async Task CustomerOnlyOrder_WithMatchingTotals_CreatesInvoice()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(async db =>
        {
            await SeedOrder(db);
            db.ServiceOrderDetails.Add(new ServiceOrderDetail { Id = 902, ServiceOrderId = 900, ItemType = "LABOR", ItemName = "Canceled", Unit = "job", PayerType = "INSURANCE", ServiceSource = "INITIAL", Status = "CANCELED", Quantity = 1, SubTotal = 90, FinalAmount = 90 });
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        var result = await service.EnsureForPaymentContextAsync(900, null, "CUSTOMER", 100);
        Assert.NotNull(result);
        Assert.Equal(100, result.TotalAmount);
        Assert.Equal(100, result.PayerId);
        var line = Assert.Single(await scope.ServiceProvider.GetRequiredService<GarageContext>().InvoiceDetails.ToListAsync());
        Assert.Equal(901, line.ReferenceServiceOrderDetailId);
    }
}