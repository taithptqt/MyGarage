using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class DomainInvariantsTests
{
    [Fact]
    public async Task MagicBytesFileSecurityScanner_BlocksDangerousOrSpoofedFiles_AndAllowsValidSignatures()
    {
        var scanner = new MagicBytesFileSecurityScanner();

        var svgAttack = await scanner.ScanAsync(new FileSecurityScanRequest
        {
            FileName = "avatar.svg",
            ContentType = "image/svg+xml",
            FileBytes = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>")
        });
        Assert.False(svgAttack.IsClean);

        var spoofedJpg = await scanner.ScanAsync(new FileSecurityScanRequest
        {
            FileName = "fake.jpg",
            ContentType = "image/jpeg",
            FileBytes = Encoding.UTF8.GetBytes("MZ\x90\x00ThisIsAnExecutableNotJpeg")
        });
        Assert.False(spoofedJpg.IsClean);

        var validPng = await scanner.ScanAsync(new FileSecurityScanRequest
        {
            FileName = "photo.png",
            ContentType = "image/png",
            FileBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00]
        });
        Assert.True(validPng.IsClean);
    }

    [Fact]
    public void PiiAnonymizerService_RedactsPhoneEmailAndLicensePlate()
    {
        var anonymizer = new PiiAnonymizerService();
        var raw = "Khach hang test.user@gmail.com SDT 0912345678 bien so 30A-12345";

        var sanitized = anonymizer.AnonymizeText(raw);

        Assert.DoesNotContain("test.user@gmail.com", sanitized);
        Assert.DoesNotContain("0912345678", sanitized);
        Assert.DoesNotContain("30A-12345", sanitized);
    }

    [Fact]
    public async Task PaymentConfirmationService_DeductsRetailStock_WhenRetailOrderConfirmed()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Warehouses.Add(new Warehouse { Id = 10, BranchId = 1, Code = "WH01", Name = "Kho ban le", Type = "SELLABLE", IsActive = true });
            db.Products.Add(new Product { Id = 50, Sku = "OIL-5W30", Name = "Dau nhot 5W-30", Unit = "Chai", CostPrice = 100_000m, RetailPrice = 200_000m });
            db.Inventories.Add(new Inventory
            {
                Id = 500,
                WarehouseId = 10,
                ProductId = 50,
                Quantity = 10m,
                ReservedQuantity = 0m,
                CurrentMacPrice = 100_000m
            });
            db.RetailOrders.Add(new RetailOrder
            {
                Id = 700,
                OrderCode = "RO-TEST-001",
                BranchId = 1,
                CustomerId = 100,
                SubTotal = 400_000m,
                FinalAmount = 400_000m,
                Status = "DRAFT",
                RetailOrderItems =
                {
                    new RetailOrderItem
                    {
                        Id = 701,
                        RetailOrderId = 700,
                        ProductId = 50,
                        Quantity = 2m,
                        UnitPriceAtTime = 200_000m,
                        SubTotal = 400_000m,
                        FinalAmount = 400_000m
                    }
                }
            });
            db.Payments.Add(new Payment
            {
                Id = 800,
                TransactionCode = "TXN-800",
                RetailOrderId = 700,
                PayerType = "CUSTOMER",
                PayerId = 100,
                MethodId = 1,
                Type = "FULL",
                Direction = "INFLOW",
                Amount = 400_000m,
                Status = "PENDING"
            });
            return Task.CompletedTask;
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var paymentConfirmation = scope.ServiceProvider.GetRequiredService<IPaymentConfirmationService>();
        var payment = await db.Payments.FirstAsync(x => x.Id == 800);

        await paymentConfirmation.ApplyOutcomeAsync(payment, 400_000m, "COMPLETED");

        var retailOrder = await db.RetailOrders.AsNoTracking().FirstAsync(x => x.Id == 700);
        var inventory = await db.Inventories.AsNoTracking().FirstAsync(x => x.Id == 500);
        Assert.Equal("CONFIRMED", retailOrder.Status);
        Assert.Equal(8m, inventory.Quantity);
    }

    [Fact]
    public async Task ServiceOrderUpdateWorkshop_PreservesRelationalServiceOrderDiscounts()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleModels.Add(new VehicleModel { Id = 11, BrandId = 1, ModelName = "Sedan" });
            db.Vehicles.Add(new Vehicle { Id = 20, ModelId = 11, LicensePlate = "51H-88888" });
            db.StandardServices.Add(new StandardService
            {
                Id = 30,
                Code = "SRV-030",
                Name = "Bao duong cap 1",
                CategoryId = 1,
                BasePrice = 1_000_000m,
                StandardMinutes = 60,
                IsActive = true
            });
            db.ServiceOrders.Add(new ServiceOrder
            {
                Id = 900,
                Code = "SO-TEST-900",
                CustomerId = 100,
                VehicleId = 20,
                ExecuteBranchId = 1,
                Status = "CHECKED_IN",
                SubTotalAmount = 1_000_000m,
                FinalAmount = 850_000m,
                ServiceOrderDiscounts =
                {
                    new ServiceOrderDiscount
                    {
                        Id = 901,
                        ServiceOrderId = 900,
                        DiscountType = "MANUAL",
                        DiscountValue = 150_000m,
                        ValueType = "FIXED",
                        AppliedAmount = 150_000m,
                        AppliedBy = 200
                    }
                }
            });
            return Task.CompletedTask;
        });

        using var scope = factory.Services.CreateScope();
        var serviceOrderService = scope.ServiceProvider.GetRequiredService<IServiceOrderService>();

        var updated = await serviceOrderService.UpdateWorkshopAsync(900, new WorkshopServiceOrderEditRequestDto
        {
            ServiceIds = [30]
        });

        Assert.NotNull(updated);
        Assert.Equal(1_000_000m, updated!.SubTotalAmount);
        Assert.Equal(850_000m, updated.FinalAmount);
    }
}
