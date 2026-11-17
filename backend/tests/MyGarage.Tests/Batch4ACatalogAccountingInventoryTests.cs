using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Repositories;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class Batch4ACatalogAccountingInventoryTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private static GarageContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new GarageContext(options);
    }

    [Fact]
    public async Task ModelSpecificRuleService_DtoMappingAndCompositeKeyNormalization_WorkAsExpected()
    {
        var dbName = $"ModelSpecificRule_{Guid.NewGuid():N}";
        await using var context = CreateInMemoryContext(dbName);
        var repository = new ModelSpecificRuleRepository(context);
        var service = new ModelSpecificRuleService(repository);

        var created = await service.CreateDtoAsync(new ModelSpecificRuleCreateDto
        {
            ModelId = 10,
            ProductId = 20,
            StandardCapacity = 4.25m,
            Tolerance = 0.15m
        });

        Assert.Equal(10, created.ModelId);
        Assert.Equal(20, created.ProductId);
        Assert.Equal(4.25m, created.StandardCapacity);
        Assert.Equal(0.15m, created.Tolerance);

        var json = JsonSerializer.Serialize(created, WebJsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(4, root.EnumerateObject().Count());
        Assert.Equal(10, root.GetProperty("modelId").GetInt64());
        Assert.Equal(20, root.GetProperty("productId").GetInt64());
        Assert.Equal(4.25m, root.GetProperty("standardCapacity").GetDecimal());
        Assert.Equal(0.15m, root.GetProperty("tolerance").GetDecimal());

        context.ChangeTracker.Clear();

        var updated = await service.UpdateDtoAsync(
            10,
            20,
            new ModelSpecificRuleUpdateDto
            {
                ModelId = 999,
                ProductId = 888,
                StandardCapacity = 5.50m,
                Tolerance = 0.30m
            });

        Assert.NotNull(updated);
        Assert.Equal(10, updated!.ModelId);
        Assert.Equal(20, updated.ProductId);
        Assert.Equal(5.50m, updated.StandardCapacity);
        Assert.Equal(0.30m, updated.Tolerance);

        var missingUpdate = await service.UpdateDtoAsync(
            10,
            999,
            new ModelSpecificRuleUpdateDto
            {
                StandardCapacity = 1.0m,
                Tolerance = 0.1m
            });
        Assert.Null(missingUpdate);

        var fetched = await service.GetDtoByKeyAsync(10, 20);
        Assert.NotNull(fetched);
        Assert.Equal(5.50m, fetched!.StandardCapacity);

        Assert.True(await service.DeleteByKeyAsync(10, 20));
        Assert.Null(await service.GetDtoByKeyAsync(10, 20));
    }

    [Fact]
    public async Task ModelSpecificRuleController_CrudEndpoints_NormalizeCompositeKeyAndReturnDtoJson()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "TOY", Name = "Toyota" });
            db.VehicleModels.Add(new VehicleModel { Id = 10, BrandId = 1, ModelName = "Camry" });
            db.Products.Add(new Product { Id = 20, Sku = "OIL-1", Name = "Dầu nhớt", Unit = "Lít", CostPrice = 120000m });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        using var createRes = await client.PostAsJsonAsync("/api/v1/model-specific-rules", new
        {
            modelId = 10,
            productId = 20,
            standardCapacity = 4.5m,
            tolerance = 0.2m
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        using var createJson = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        Assert.Equal(10, createJson.RootElement.GetProperty("modelId").GetInt64());
        Assert.Equal(20, createJson.RootElement.GetProperty("productId").GetInt64());
        Assert.Equal(4.5m, createJson.RootElement.GetProperty("standardCapacity").GetDecimal());
        Assert.Equal(0.2m, createJson.RootElement.GetProperty("tolerance").GetDecimal());

        using var updateRes = await client.PutAsJsonAsync("/api/v1/model-specific-rules/10/20", new
        {
            modelId = 999,
            productId = 888,
            standardCapacity = 4.8m,
            tolerance = 0.25m
        });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        using var updateJson = JsonDocument.Parse(await updateRes.Content.ReadAsStringAsync());
        Assert.Equal(10, updateJson.RootElement.GetProperty("modelId").GetInt64());
        Assert.Equal(20, updateJson.RootElement.GetProperty("productId").GetInt64());
        Assert.Equal(4.8m, updateJson.RootElement.GetProperty("standardCapacity").GetDecimal());

        using var getRes = await client.GetAsync("/api/v1/model-specific-rules/10/20");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        using var deleteRes = await client.DeleteAsync("/api/v1/model-specific-rules/10/20");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        using var getDeletedRes = await client.GetAsync("/api/v1/model-specific-rules/10/20");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedRes.StatusCode);
    }

    [Fact]
    public async Task PackageService_AppliesDefaultsTrimsStringsAndMapsSortedItems()
    {
        var dbName = $"PackageService_{Guid.NewGuid():N}";
        await using var context = CreateInMemoryContext(dbName);
        context.ServiceCategories.Add(new ServiceCategory
        {
            Id = 1,
            Code = "MAINT",
            Name = "Bảo dưỡng",
            IsActive = true,
            IsDeleted = false
        });
        context.StandardServices.AddRange(
            new StandardService
            {
                Id = 10,
                Code = "SRV-A",
                Name = "Thay dầu động cơ",
                CategoryId = 1,
                BasePrice = 200000m,
                StandardMinutes = 30
            },
            new StandardService
            {
                Id = 11,
                Code = "SRV-B",
                Name = "Kiểm tra phanh",
                CategoryId = 1,
                BasePrice = 100000m,
                StandardMinutes = 20
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new PackageRepository(context);
        var service = new PackageService(repository);

        var created = await service.CreateFromDtoAsync(new PackageCreateDto
        {
            Code = "  PKG-BASIC  ",
            Name = "  Gói bảo dưỡng cơ bản  ",
            CategoryId = 1,
            BasePrice = 500000m,
            StandardMinutes = 60,
            IsActive = null,
            Items = new List<PackageItemRequestDto>
            {
                new() { StandardServiceId = 0, Quantity = 2m, SortOrder = 1, IsDefault = true },
                new() { StandardServiceId = 11, Quantity = 0m, SortOrder = 20, IsDefault = null },
                new() { StandardServiceId = 10, Quantity = -1m, SortOrder = null, IsDefault = false }
            }
        });

        Assert.Equal("PKG-BASIC", created.Code);
        Assert.Equal("Gói bảo dưỡng cơ bản", created.Name);
        Assert.True(created.IsActive);
        Assert.Equal("MAINT", created.CategoryCode);
        Assert.Equal("Bảo dưỡng", created.CategoryName);
        Assert.Equal(2, created.Items.Count);

        var firstItem = created.Items[0];
        var secondItem = created.Items[1];
        Assert.Equal(10, firstItem.StandardServiceId);
        Assert.Equal(1m, firstItem.Quantity);
        Assert.Equal(0, firstItem.SortOrder);
        Assert.False(firstItem.IsDefault);
        Assert.True(firstItem.IsActive);

        Assert.Equal(11, secondItem.StandardServiceId);
        Assert.Equal(1m, secondItem.Quantity);
        Assert.Equal(20, secondItem.SortOrder);
        Assert.True(secondItem.IsDefault);

        context.ChangeTracker.Clear();

        var updated = await service.UpdateFromDtoAsync(created.Id, new PackageUpdateDto
        {
            Code = "  PKG-PRO  ",
            Name = "  Gói bảo dưỡng nâng cao  ",
            CategoryId = 1,
            BasePrice = 750000m,
            StandardMinutes = 90,
            Description = "Đã cập nhật",
            IsActive = false
        });

        Assert.NotNull(updated);
        Assert.Equal("PKG-PRO", updated!.Code);
        Assert.Equal("Gói bảo dưỡng nâng cao", updated.Name);
        Assert.Equal(750000m, updated.BasePrice);
        Assert.Equal(90, updated.StandardMinutes);
        Assert.False(updated.IsActive);

        Assert.Null(await service.UpdateFromDtoAsync(99999, new PackageUpdateDto
        {
            Code = "MISSING",
            Name = "Missing",
            CategoryId = 1
        }));
    }

    [Fact]
    public async Task PackagesController_PublicGetAndAdminCrud_WorkEndToEnd()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceCategories.Add(new ServiceCategory { Id = 1, Code = "CAT-1", Name = "Sửa chữa", IsActive = true, IsDeleted = false });
            db.StandardServices.Add(new StandardService { Id = 10, Code = "SRV-1", Name = "Cân bằng động", CategoryId = 1, BasePrice = 150000m, StandardMinutes = 25 });
            return Task.CompletedTask;
        });

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));

        using var createRes = await adminClient.PostAsJsonAsync("/api/v1/packages", new
        {
            code = " PKG-HTTP ",
            name = " Gói kiểm tra tổng quát ",
            categoryId = 1,
            basePrice = 300000m,
            standardMinutes = 45,
            items = new[]
            {
                new { standardServiceId = 10, quantity = 0m }
            }
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        using var createJson = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var packageId = createJson.RootElement.GetProperty("id").GetInt64();
        Assert.Equal("PKG-HTTP", createJson.RootElement.GetProperty("code").GetString());
        Assert.Equal("Gói kiểm tra tổng quát", createJson.RootElement.GetProperty("name").GetString());
        Assert.Equal(1m, createJson.RootElement.GetProperty("items")[0].GetProperty("quantity").GetDecimal());

        using var anonClient = factory.CreateClient();
        using var getAllRes = await anonClient.GetAsync("/api/v1/packages");
        Assert.Equal(HttpStatusCode.OK, getAllRes.StatusCode);
        using var getByIdRes = await anonClient.GetAsync($"/api/v1/packages/{packageId}");
        Assert.Equal(HttpStatusCode.OK, getByIdRes.StatusCode);

        using var deleteRes = await adminClient.DeleteAsync($"/api/v1/packages/{packageId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);
    }

    [Fact]
    public void MaterialRequestService_AutoIssuanceRemainderAndUnitCostFallback_FollowContract()
    {
        var details = new List<MaterialRequestDetail>
        {
            new() { Id = 1, ProductId = 10, WarehouseId = 100, RequestedQuantity = 5m, IssuedQuantity = 2m, Status = "PARTIALLY_ISSUED" },
            new() { Id = 2, ProductId = 11, WarehouseId = 100, RequestedQuantity = 3m, IssuedQuantity = 3m, Status = "ISSUED" },
            new() { Id = 3, ProductId = 12, WarehouseId = 100, RequestedQuantity = 4m, IssuedQuantity = null, Status = "PENDING" }
        };

        var autoItems = MaterialRequestService.ResolveIssuanceItems(details, null);
        Assert.Equal(2, autoItems.Count);
        Assert.Equal(1, autoItems[0].DetailId);
        Assert.Equal(3m, autoItems[0].QuantityToIssue);
        Assert.Equal(3, autoItems[1].DetailId);
        Assert.Equal(4m, autoItems[1].QuantityToIssue);

        var explicitItems = MaterialRequestService.ResolveIssuanceItems(
            details,
            new List<MaterialRequestIssuanceItemDto>
            {
                new() { DetailId = 0, QuantityToIssue = 1m },
                new() { DetailId = 1, QuantityToIssue = 1.5m }
            });
        Assert.Equal(2, explicitItems.Count);
        Assert.Equal(0, explicitItems[0].DetailId);
        Assert.Equal(1.5m, explicitItems[1].QuantityToIssue);
        Assert.Empty(MaterialRequestService.ResolveIssuanceItems(details, new List<MaterialRequestIssuanceItemDto>()));

        var productWithCost = new Product { Id = 10, Sku = "SKU-1", Name = "Lọc dầu", Unit = "Cái", CostPrice = 80000m };
        var productWithoutCost = new Product { Id = 11, Sku = "SKU-2", Name = "Gioăng", Unit = "Cái", CostPrice = null };

        Assert.Equal(
            125000m,
            MaterialRequestService.ResolveUnitCost(
                new Inventory { ProductId = 10, WarehouseId = 100, Quantity = 5m, CurrentMacPrice = 125000m },
                productWithCost));
        Assert.Equal(
            80000m,
            MaterialRequestService.ResolveUnitCost(
                new Inventory { ProductId = 10, WarehouseId = 100, Quantity = 5m, CurrentMacPrice = null },
                productWithCost));
        Assert.Equal(0m, MaterialRequestService.ResolveUnitCost(null, productWithoutCost));

        var requestDate = new DateTime(2026, 11, 1, 8, 30, 0, DateTimeKind.Utc);
        var mr = new MaterialRequest
        {
            Id = 50,
            ServiceOrderId = 500,
            ServiceOrder = new ServiceOrder { Id = 500, Code = "SO-500", Status = "IN_PROGRESS" },
            TechnicianId = 200,
            Technician = new Staff { Id = 200, FullName = "KTV A", Phone = "0900", Email = "ktv@test", PasswordHash = "h" },
            RequestDate = requestDate,
            CreatedAt = null,
            Status = "PENDING",
            IsRework = false,
            Note = "Cần gấp",
            MaterialRequestDetails = new List<MaterialRequestDetail>
            {
                new()
                {
                    Id = 1,
                    ProductId = 10,
                    WarehouseId = 100,
                    Product = productWithCost,
                    RequestedQuantity = 5m,
                    IssuedQuantity = null,
                    Status = "PENDING"
                }
            }
        };

        var stockLookup = new Dictionary<(long ProductId, long WarehouseId), Inventory>
        {
            [(10, 100)] = new Inventory { ProductId = 10, WarehouseId = 100, Quantity = 12m, CurrentMacPrice = null }
        };

        var summary = MaterialRequestService.MapSummaryDto(mr, stockLookup);
        Assert.Equal(requestDate, summary.CreatedAt);
        Assert.Equal("Cần gấp", summary.Note);
        Assert.Equal("Cần gấp", summary.Notes);
        var mappedItem = Assert.Single(summary.Items);
        Assert.Equal("SKU-1", mappedItem.ProductCode);
        Assert.Equal("SKU-1", mappedItem.Sku);
        Assert.Equal(5m, mappedItem.Quantity);
        Assert.Equal(5m, mappedItem.RequestedQuantity);
        Assert.Equal(0m, mappedItem.IssuedQuantity);
        Assert.Equal(12m, mappedItem.CurrentStock);
        Assert.Equal(80000m, mappedItem.UnitCost);
    }

    [Fact]
    public async Task MaterialRequestController_AutoIssuanceRemainderLocksAndCostFallback_WorkEndToEnd()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "TOY", Name = "Toyota" });
            db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "Vios" });
            db.Vehicles.Add(new Vehicle { Id = 1, LicensePlate = "51A-99999", ModelId = 1 });
            db.ServiceOrders.AddRange(
                new ServiceOrder { Id = 9001, Code = "SO-9001", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "IN_PROGRESS" },
                new ServiceOrder { Id = 9002, Code = "SO-9002", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "CLOSED" },
                new ServiceOrder { Id = 9003, Code = "SO-9003", CustomerId = 100, VehicleId = 1, OriginBranchId = 2, ExecuteBranchId = 2, Status = "IN_PROGRESS" });
            db.Warehouses.Add(new Warehouse { Id = 9101, BranchId = 1, Code = "WH-1", Name = "Kho chính CN1", Type = "MAIN" });
            db.Products.AddRange(
                new Product { Id = 9201, Sku = "OIL-5W30", Name = "Nhớt 5W30", Unit = "Lít", CostPrice = 150000m },
                new Product { Id = 9202, Sku = "FIL-01", Name = "Lọc nhớt", Unit = "Cái", CostPrice = 80000m });
            db.Inventories.AddRange(
                new Inventory { Id = 9301, WarehouseId = 9101, ProductId = 9201, Quantity = 10m, ReservedQuantity = 3m, CurrentMacPrice = 175000m },
                new Inventory { Id = 9302, WarehouseId = 9101, ProductId = 9202, Quantity = 10m, ReservedQuantity = 0m, CurrentMacPrice = null });
            db.MaterialRequests.AddRange(
                new MaterialRequest
                {
                    Id = 9401,
                    ServiceOrderId = 9001,
                    TechnicianId = 200,
                    RequestDate = DateTime.UtcNow,
                    Status = "PARTIALLY_ISSUED",
                    Note = "Xuất bù",
                    MaterialRequestDetails = new List<MaterialRequestDetail>
                    {
                        new() { Id = 9501, RequestId = 9401, ProductId = 9201, WarehouseId = 9101, RequestedQuantity = 5m, ReservedQuantity = 3m, IssuedQuantity = 2m, Status = "PARTIALLY_ISSUED" },
                        new() { Id = 9502, RequestId = 9401, ProductId = 9202, WarehouseId = 9101, RequestedQuantity = 2m, ReservedQuantity = 0m, IssuedQuantity = 2m, Status = "ISSUED" }
                    }
                },
                new MaterialRequest
                {
                    Id = 9402,
                    ServiceOrderId = 9001,
                    TechnicianId = 200,
                    RequestDate = DateTime.UtcNow,
                    Status = "PARTIALLY_ISSUED",
                    MaterialRequestDetails = new List<MaterialRequestDetail>
                    {
                        new() { Id = 9503, RequestId = 9402, ProductId = 9202, WarehouseId = 9101, RequestedQuantity = 2m, IssuedQuantity = 2m, Status = "ISSUED" }
                    }
                },
                new MaterialRequest
                {
                    Id = 9403, ServiceOrderId = 9001, TechnicianId = 200, Status = "REJECTED",
                    MaterialRequestDetails = new List<MaterialRequestDetail>
                    {
                        new() { Id = 9504, RequestId = 9403, ProductId = 9201, WarehouseId = 9101, RequestedQuantity = 1m, Status = "REJECTED" }
                    }
                });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        using var crossBranchCreateRes = await client.PostAsJsonAsync("/api/v1/material-requests", new
        {
            serviceOrderId = 9003,
            note = "Cross branch"
        });
        Assert.Equal(HttpStatusCode.Forbidden, crossBranchCreateRes.StatusCode);

        using var closedSoCreateRes = await client.PostAsJsonAsync("/api/v1/material-requests", new
        {
            serviceOrderId = 9002,
            note = "Closed SO"
        });
        Assert.Equal(HttpStatusCode.BadRequest, closedSoCreateRes.StatusCode);

        using var excessRes = await client.PostAsJsonAsync("/api/v1/material-requests/9401/issuances", new
        {
            items = new[] { new { detailId = 9501, quantityToIssue = 4m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, excessRes.StatusCode);

        using var rejectedRes = await client.PostAsJsonAsync("/api/v1/material-requests/9403/issuances", new { });
        Assert.Equal(HttpStatusCode.BadRequest, rejectedRes.StatusCode);

        using var issueRes = await client.PostAsJsonAsync("/api/v1/material-requests/9401/issuances", new
        {
            note = "Xuất phần còn lại"
        });
        Assert.Equal(HttpStatusCode.OK, issueRes.StatusCode);

        using var issueJson = JsonDocument.Parse(await issueRes.Content.ReadAsStringAsync());
        Assert.Equal("ISSUED", issueJson.RootElement.GetProperty("status").GetString());
        var items = issueJson.RootElement.GetProperty("items").EnumerateArray().ToList();
        var itemOil = items.Single(x => x.GetProperty("productId").GetInt64() == 9201);
        var itemFilter = items.Single(x => x.GetProperty("productId").GetInt64() == 9202);
        Assert.Equal(5m, itemOil.GetProperty("issuedQuantity").GetDecimal());
        Assert.Equal(175000m, itemOil.GetProperty("unitCost").GetDecimal());
        Assert.Equal(7m, itemOil.GetProperty("currentStock").GetDecimal());
        Assert.Equal(2m, itemFilter.GetProperty("issuedQuantity").GetDecimal());
        Assert.Equal(80000m, itemFilter.GetProperty("unitCost").GetDecimal());
        Assert.Equal(10m, itemFilter.GetProperty("currentStock").GetDecimal());

        using var noRemainingRes = await client.PostAsJsonAsync("/api/v1/material-requests/9402/issuances", new
        {
            note = "Không còn vật tư"
        });
        Assert.Equal(HttpStatusCode.BadRequest, noRemainingRes.StatusCode);
        using var noRemainingJson = JsonDocument.Parse(await noRemainingRes.Content.ReadAsStringAsync());
        Assert.Equal("Phiếu yêu cầu không còn vật tư cần xuất.", noRemainingJson.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvoiceDetailServiceAndController_EnforceClosedAndBillingLocksWhilePreservingBranchScope()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "TOY", Name = "Toyota" });
            db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "Fortuner" });
            db.Vehicles.Add(new Vehicle { Id = 1, LicensePlate = "51A-88888", ModelId = 1 });
            db.ServiceOrders.AddRange(
                new ServiceOrder { Id = 9601, Code = "SO-9601", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "CLOSED" },
                new ServiceOrder { Id = 9602, Code = "SO-9602", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "WAITING_PAYMENT" },
                new ServiceOrder { Id = 9603, Code = "SO-9603", CustomerId = 100, VehicleId = 1, OriginBranchId = 2, ExecuteBranchId = 2, Status = "WAITING_PAYMENT" });
            db.RetailOrders.Add(new RetailOrder { Id = 9604, BranchId = 1, OrderCode = "RO-9604" });
            db.Invoices.AddRange(
                new Invoice { Id = 9701, ServiceOrderId = 9601, PayerType = "CUSTOMER" },
                new Invoice { Id = 9702, ServiceOrderId = 9602, PayerType = "CUSTOMER" },
                new Invoice { Id = 9703, ServiceOrderId = 9603, PayerType = "CUSTOMER" },
                new Invoice { Id = 9704, RetailOrderId = 9604, PayerType = "CUSTOMER" });
            db.InvoiceDetails.AddRange(
                new InvoiceDetail { Id = 9801, InvoiceId = 9701, ItemType = "SERVICE", ItemName = "Locked", Unit = "unit", Quantity = 1m, UnitPriceAtTime = 100m, SubTotal = 100m, FinalAmount = 100m },
                new InvoiceDetail { Id = 9802, InvoiceId = 9702, ItemType = "SERVICE", ItemName = "Công thợ", Unit = "giờ", Quantity = 1m, UnitPriceAtTime = 100m, SubTotal = 100m, FinalAmount = 100m },
                new InvoiceDetail { Id = 9803, InvoiceId = 9703, ItemType = "SERVICE", ItemName = "Công thợ CN2", Unit = "giờ", Quantity = 1m, UnitPriceAtTime = 100m, SubTotal = 100m, FinalAmount = 100m },
                new InvoiceDetail { Id = 9804, InvoiceId = 9704, ItemType = "SERVICE", ItemName = "Open", Unit = "unit", Quantity = 1m, UnitPriceAtTime = 100m, SubTotal = 100m, FinalAmount = 100m });
            return Task.CompletedTask;
        });

        using (var scope = factory.Services.CreateScope())
        {
            var detailService = scope.ServiceProvider.GetRequiredService<IInvoiceDetailService>();
            var closedError = await detailService.ValidateInvoiceDetailMutationAsync(9701);
            Assert.Equal("Không thể chỉnh sửa chi tiết hóa đơn vì service order đã đóng.", closedError);

            var billingError = await detailService.ValidateInvoiceDetailMutationAsync(null, 9702);
            Assert.Equal("Không thể chỉnh sửa chi tiết hóa đơn vì lệnh đã chốt tài chính.", billingError);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        using var closedCreateRes = await client.PostAsJsonAsync("/api/v1/invoice-details", new
        {
            invoiceId = 9701,
            itemType = "SERVICE",
            itemName = "Kiểm tra",
            unit = "lần",
            quantity = 1,
            unitPriceAtTime = 50,
            subTotal = 50,
            finalAmount = 50
        });
        Assert.Equal(HttpStatusCode.BadRequest, closedCreateRes.StatusCode);
        using var moveFromLocked = await client.PutAsJsonAsync("/api/v1/invoice-details/9801", new
        {
            invoiceId = 9704, itemType = "SERVICE", itemName = "Moved", unit = "unit",
            quantity = 1, unitPriceAtTime = 100, subTotal = 100, finalAmount = 100
        });
        Assert.Equal(HttpStatusCode.BadRequest, moveFromLocked.StatusCode);
        using var lockedDetail = await client.GetAsync("/api/v1/invoice-details/9801");
        using var lockedDetailJson = JsonDocument.Parse(await lockedDetail.Content.ReadAsStringAsync());
        Assert.Equal(9701, lockedDetailJson.RootElement.GetProperty("invoiceId").GetInt64());
        using var moveToLocked = await client.PutAsJsonAsync("/api/v1/invoice-details/9804", new
        {
            invoiceId = 9701, itemType = "SERVICE", itemName = "Moved", unit = "unit",
            quantity = 1, unitPriceAtTime = 100, subTotal = 100, finalAmount = 100
        });
        Assert.Equal(HttpStatusCode.BadRequest, moveToLocked.StatusCode);
        using var closedCreateJson = JsonDocument.Parse(await closedCreateRes.Content.ReadAsStringAsync());
        Assert.Equal("Không thể chỉnh sửa chi tiết hóa đơn vì service order đã đóng.", closedCreateJson.RootElement.GetProperty("detail").GetString());

        using var billingUpdateRes = await client.PutAsJsonAsync("/api/v1/invoice-details/9802", new
        {
            invoiceId = 9702,
            itemName = "Công thợ sửa"
        });
        Assert.Equal(HttpStatusCode.BadRequest, billingUpdateRes.StatusCode);
        using var billingUpdateJson = JsonDocument.Parse(await billingUpdateRes.Content.ReadAsStringAsync());
        Assert.Equal("Không thể chỉnh sửa chi tiết hóa đơn vì lệnh đã chốt tài chính.", billingUpdateJson.RootElement.GetProperty("detail").GetString());

        using var billingDeleteRes = await client.DeleteAsync("/api/v1/invoice-details/9802");
        Assert.Equal(HttpStatusCode.BadRequest, billingDeleteRes.StatusCode);
        using var billingDeleteJson = JsonDocument.Parse(await billingDeleteRes.Content.ReadAsStringAsync());
        Assert.Equal("Không thể chỉnh sửa chi tiết hóa đơn vì lệnh đã chốt tài chính.", billingDeleteJson.RootElement.GetProperty("detail").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/invoice-details/9803")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/v1/invoice-details/9803", new { invoiceId = 9703, itemName = "Cross branch" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/v1/invoice-details/9803")).StatusCode);
    }
}
