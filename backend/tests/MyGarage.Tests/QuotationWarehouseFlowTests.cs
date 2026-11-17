using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class QuotationWarehouseFlowTests
{
    [Fact]
    public void HistoricalReservationIsNotGuessedWhenStockHasReservations()
    {
        var line = new MaterialRequestDetail { RequestedQuantity = 2, ReservedQuantity = null };
        Assert.Throws<InvalidOperationException>(() => MyGarage.Services.MaterialRequestService.ResolveLineReservation(line, 1));
        Assert.Equal(0m, MyGarage.Services.MaterialRequestService.ResolveLineReservation(line, 0));
    }

    [Fact]
    public async Task RevisedQuotationWithUnlinkedHistoricalRequestRequiresReconciliation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceOrders.Add(new ServiceOrder { Id = 7300, Code = "SO-7300", CustomerId = 100, OriginBranchId = 1, ExecuteBranchId = 1, Status = "IN_PROGRESS" });
            db.Warehouses.Add(new Warehouse { Id = 7301, BranchId = 1, Code = "WH-7301", Name = "Kho", Type = "MAIN" });
            db.Products.Add(new Product { Id = 7302, Sku = "TEST-7302", Name = "Phụ tùng", Unit = "cái" });
            db.Quotations.Add(new Quotation { Id = 7304, ServiceOrderId = 7300, Code = "QT-7304", Status = "CUSTOMER_APPROVED", QuotationType = "REPAIR", InsuranceChangeType = "NONE" });
            db.QuotationDetails.Add(new QuotationDetail { Id = 7305, QuotationId = 7304, SourceQuotationDetailId = 7000, ServiceOrderId = 7300, ItemType = "PART", ItemId = 7302, ItemName = "Phụ tùng", Quantity = 2, PayerType = "CUSTOMER", Status = "ACTIVE", ApprovalStatus = "APPROVED" });
            db.ServiceOrderDetails.Add(new ServiceOrderDetail { Id = 7306, ServiceOrderId = 7300, SourceQuotationId = 7304, QuotationDetailId = 7305, ItemType = "PART", ItemId = 7302, ItemName = "Phụ tùng", Unit = "cái", PayerType = "CUSTOMER", ServiceSource = "INITIAL", Quantity = 2, Status = "APPROVED" });
            db.MaterialRequests.Add(new MaterialRequest { Id = 7307, ServiceOrderId = 7300, Status = "PENDING", MaterialRequestDetails = new List<MaterialRequestDetail> { new() { Id = 7308, ProductId = 7302, WarehouseId = 7301, RequestedQuantity = 2, Status = "PENDING" } } });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMaterialRequestService>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFromQuotationAsync(7304, 0));
        Assert.Contains("đối soát", error.Message);
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal(1, await db.MaterialRequests.CountAsync());
    }

    [Fact]
    public async Task IssuingAndRejectingOnlyReleaseTheirOwnReservation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Warehouses.Add(new Warehouse { Id = 7201, BranchId = 1, Code = "WH-7201", Name = "Kho", Type = "MAIN" });
            db.Products.Add(new Product { Id = 7202, Sku = "TEST-7202", Name = "Phụ tùng", Unit = "cái" });
            db.Inventories.Add(new Inventory { Id = 7203, WarehouseId = 7201, ProductId = 7202, Quantity = 3, ReservedQuantity = 3 });
            db.MaterialRequests.AddRange(
                new MaterialRequest { Id = 7204, Status = "PENDING", MaterialRequestDetails = new List<MaterialRequestDetail> { new() { Id = 7205, ProductId = 7202, WarehouseId = 7201, RequestedQuantity = 2, ReservedQuantity = 1, Status = "PENDING" } } },
                new MaterialRequest { Id = 7206, Status = "PENDING", MaterialRequestDetails = new List<MaterialRequestDetail> { new() { Id = 7207, ProductId = 7202, WarehouseId = 7201, RequestedQuantity = 2, ReservedQuantity = 2, Status = "PENDING" } } });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMaterialRequestService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.IssuePartsAsync(new MaterialRequestIssuanceDto
        {
            RequestId = 7204, ActorStaffId = 200, Items = new() { new() { DetailId = 7205, QuantityToIssue = 2 } }
        }));
        await service.RejectRequestAsync(7204, 200, "Kiểm thử");
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal(2m, (await db.Inventories.SingleAsync(x => x.Id == 7203)).ReservedQuantity);
        await service.IssuePartsAsync(new MaterialRequestIssuanceDto
        {
            RequestId = 7206, ActorStaffId = 200, Items = new() { new() { DetailId = 7207, QuantityToIssue = 2 } }
        });
        Assert.Equal(0m, (await db.Inventories.SingleAsync(x => x.Id == 7203)).ReservedQuantity);
        Assert.Equal(1m, (await db.Inventories.SingleAsync(x => x.Id == 7203)).Quantity);
    }

    [Fact]
    public async Task CreatingWarehouseRequestAgainDoesNotDuplicateAndIncreasesOnlyByDifference()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceOrders.Add(new ServiceOrder { Id = 7100, Code = "SO-7100", CustomerId = 100, OriginBranchId = 1, ExecuteBranchId = 1, Status = "IN_PROGRESS" });
            db.Warehouses.Add(new Warehouse { Id = 7101, BranchId = 1, Code = "WH-7101", Name = "Kho kiểm thử", Type = "MAIN" });
            db.Products.Add(new Product { Id = 7102, Sku = "TEST-7102", Name = "Phụ tùng", Unit = "cái" });
            db.Inventories.Add(new Inventory { Id = 7103, WarehouseId = 7101, ProductId = 7102, Quantity = 10m, ReservedQuantity = 0m });
            db.Quotations.Add(new Quotation { Id = 7104, ServiceOrderId = 7100, Code = "QT-7104", Status = "CUSTOMER_APPROVED", QuotationType = "REPAIR", InsuranceChangeType = "NONE" });
            db.QuotationDetails.Add(new QuotationDetail { Id = 7105, QuotationId = 7104, ServiceOrderId = 7100, ItemType = "PART", ItemId = 7102, ItemName = "Phụ tùng", Quantity = 2, PayerType = "CUSTOMER", Status = "ACTIVE", ApprovalStatus = "APPROVED" });
            db.ServiceOrderDetails.Add(new ServiceOrderDetail { Id = 7106, ServiceOrderId = 7100, SourceQuotationId = 7104, QuotationDetailId = 7105, ItemType = "PART", ItemId = 7102, ItemName = "Phụ tùng", Unit = "cái", PayerType = "CUSTOMER", ServiceSource = "INITIAL", Quantity = 2, Status = "APPROVED" });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMaterialRequestService>();
        Assert.NotNull(await service.CreateFromQuotationAsync(7104, 0));
        Assert.Null(await service.CreateFromQuotationAsync(7104, 0));

        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var detail = await db.MaterialRequestDetails.SingleAsync();
        Assert.Equal(7106, detail.ServiceOrderDetailId);
        Assert.Equal(2m, detail.RequestedQuantity);
        Assert.Equal(2m, detail.ReservedQuantity);
        Assert.Null((await db.MaterialRequests.SingleAsync()).TechnicianId);
        Assert.Equal(2m, (await db.Inventories.SingleAsync(x => x.Id == 7103)).ReservedQuantity);

        var quoteLine = await db.QuotationDetails.SingleAsync(x => x.Id == 7105);
        quoteLine.Quantity = 3;
        await db.SaveChangesAsync();
        Assert.NotNull(await service.CreateFromQuotationAsync(7104, 0));
        Assert.Equal(3m, await db.MaterialRequestDetails.SumAsync(x => x.RequestedQuantity));
        Assert.Equal(3m, (await db.Inventories.SingleAsync(x => x.Id == 7103)).ReservedQuantity);

        quoteLine.Quantity = 15;
        await db.SaveChangesAsync();
        Assert.NotNull(await service.CreateFromQuotationAsync(7104, 0));
        Assert.Equal(15m, await db.MaterialRequestDetails.SumAsync(x => x.RequestedQuantity));
        Assert.Equal(10m, await db.MaterialRequestDetails.SumAsync(x => x.ReservedQuantity));
        Assert.Equal(10m, (await db.Inventories.SingleAsync(x => x.Id == 7103)).ReservedQuantity);

        quoteLine.Status = "CANCELED";
        quoteLine.SourceQuotationDetailId = 7000;
        await db.SaveChangesAsync();
        var cancellationError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFromQuotationAsync(7104, 0));
        Assert.Contains("đối soát", cancellationError.Message);
        Assert.Equal(15m, await db.MaterialRequestDetails.SumAsync(x => x.RequestedQuantity));
    }
}
