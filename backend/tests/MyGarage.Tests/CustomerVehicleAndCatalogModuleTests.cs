using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyGarage.Application.Authorization;
using MyGarage.Application.Catalog.DTOs;
using MyGarage.Application.Customers.DTOs;
using MyGarage.Application.Vehicles.DTOs;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class CustomerVehicleAndCatalogModuleTests
{
    // =========================================================================
    // SECTION 1: Customer Pure Domain Rules (CustomerDomainRules)
    // =========================================================================

    [Fact]
    public void CustomerRules_PhoneValidation_EnforcesVietnamPhone10()
    {
        // Valid VN phone formats (0xxxxxxxxx or +84xxxxxxxxx)
        Assert.True(CustomerDomainRules.IsValidVietnamPhone("0912345678"));
        Assert.True(CustomerDomainRules.IsValidVietnamPhone("0987654321"));
        Assert.True(CustomerDomainRules.IsValidVietnamPhone("+84912345678"));
        Assert.True(CustomerDomainRules.IsValidVietnamPhone("  0901234567  "));

        // Invalid formats
        Assert.False(CustomerDomainRules.IsValidVietnamPhone("123456"));
        Assert.False(CustomerDomainRules.IsValidVietnamPhone("09123456789")); // 11 digits
        Assert.False(CustomerDomainRules.IsValidVietnamPhone("081234567")); // 9 digits
        Assert.False(CustomerDomainRules.IsValidVietnamPhone("09abcde123"));
        Assert.False(CustomerDomainRules.IsValidVietnamPhone(null));
        Assert.False(CustomerDomainRules.IsValidVietnamPhone(""));
    }

    [Fact]
    public void CustomerRules_EmailValidation_NormalizesAndValidates()
    {
        Assert.Equal("test@garage.vn", CustomerDomainRules.NormalizeEmail("  TEST@GARAGE.VN "));
        Assert.True(CustomerDomainRules.IsValidEmail("customer@mygarage.vn"));
        Assert.True(CustomerDomainRules.IsValidEmail("user.name+tag@domain.com"));

        Assert.False(CustomerDomainRules.IsValidEmail("invalid-email"));
        Assert.False(CustomerDomainRules.IsValidEmail("@domain.com"));
        Assert.False(CustomerDomainRules.IsValidEmail("user@"));
        Assert.False(CustomerDomainRules.IsValidEmail(null));
    }

    [Fact]
    public void CustomerRules_PreferredBranchTag_FormatsParsesAndUpdates()
    {
        var tag = CustomerDomainRules.FormatPreferredBranchTag(5);
        Assert.Equal("[PreferredBranchId:5]", tag);

        var parsed = CustomerDomainRules.ExtractPreferredBranchId("Khách quen chi nhánh 5 [PreferredBranchId:5] ghi chú thêm");
        Assert.Equal(5, parsed);

        Assert.Null(CustomerDomainRules.ExtractPreferredBranchId("Không có tag"));
        Assert.Null(CustomerDomainRules.ExtractPreferredBranchId(null));

        var updated = CustomerDomainRules.SetPreferredBranchTag("Khách VIP", 7);
        Assert.Contains("[PreferredBranchId:7]", updated);

        var replaced = CustomerDomainRules.SetPreferredBranchTag("Ghi chú cũ [PreferredBranchId:3]", 8);
        Assert.Contains("[PreferredBranchId:8]", replaced);
        Assert.DoesNotContain("[PreferredBranchId:3]", replaced);

        var removed = CustomerDomainRules.RemovePreferredBranchTag("Ghi chú [PreferredBranchId:8]");
        Assert.DoesNotContain("[PreferredBranchId:8]", removed);
    }

    [Fact]
    public void CustomerRules_DuplicateDetection_DetectsCollisions()
    {
        var (dup1, field1, _) = CustomerDomainRules.CheckDuplicateInvariants("0912345678", "a@b.com", "0912345678", "other@b.com");
        Assert.True(dup1);
        Assert.Equal("Phone", field1);

        var (dup2, field2, _) = CustomerDomainRules.CheckDuplicateInvariants("0987654321", "dup@b.com", "0911111111", "DUP@B.COM");
        Assert.True(dup2);
        Assert.Equal("Email", field2);

        var (noDup, _, _) = CustomerDomainRules.CheckDuplicateInvariants("0912345678", "a@b.com", "0999999999", "b@b.com");
        Assert.False(noDup);
    }

    // =========================================================================
    // SECTION 2: Vehicle Pure Domain Rules (VehicleDomainRules)
    // =========================================================================

    [Fact]
    public void VehicleRules_PlateNormalizationAndValidation_BehavesCorrectly()
    {
        Assert.Equal("30A-12345", VehicleDomainRules.NormalizePlate("  30a-12345 "));
        Assert.True(VehicleDomainRules.IsValidPlate("30A-12345"));
        Assert.True(VehicleDomainRules.IsValidPlate("51F-123.45"));
        Assert.True(VehicleDomainRules.IsValidPlate("29A12345"));
        Assert.True(VehicleDomainRules.IsValidPlate("TEMP-A1B2C3D4"));

        Assert.False(VehicleDomainRules.IsValidPlate("INVALID PLATE!!"));
        Assert.False(VehicleDomainRules.IsValidPlate(null));
        Assert.False(VehicleDomainRules.IsValidPlate(""));
    }

    [Fact]
    public void VehicleRules_VinValidation_Enforces17CharactersExcludingIOQ()
    {
        Assert.Equal("1HGCR2F83HA000000", VehicleDomainRules.NormalizeVin(" 1hgcr2f83ha000000 "));
        Assert.True(VehicleDomainRules.IsValidVin("1HGCR2F83HA000000")); // 17 chars, no I, O, Q

        // Invalid: contains 'I'
        Assert.False(VehicleDomainRules.IsValidVin("1HGCR2F83IA000000"));
        // Invalid: contains 'O'
        Assert.False(VehicleDomainRules.IsValidVin("1HGCR2F83OA000000"));
        // Invalid: contains 'Q'
        Assert.False(VehicleDomainRules.IsValidVin("1HGCR2F83QA000000"));
        // Invalid: length not 17
        Assert.False(VehicleDomainRules.IsValidVin("1HGCR2F83HA"));
        Assert.False(VehicleDomainRules.IsValidVin(null));
    }

    [Fact]
    public void VehicleRules_OdoAndYearValidation_ProtectsDataIntegrity()
    {
        Assert.True(VehicleDomainRules.ValidateOdo(10000, 5000));
        Assert.False(VehicleDomainRules.ValidateOdo(-50, 0));
        Assert.False(VehicleDomainRules.ValidateOdo(4000, 5000)); // Rollback rejected

        Assert.True(VehicleDomainRules.ValidateManufactureYear(2022, 2026));
        Assert.False(VehicleDomainRules.ValidateManufactureYear(1890, 2026)); // Too old
        Assert.False(VehicleDomainRules.ValidateManufactureYear(2035, 2026)); // Too far in future
    }

    [Fact]
    public void VehicleRules_OwnershipTransitions_GeneratesCorrectFsmResult()
    {
        var today = new DateOnly(2026, 11, 14);

        // Transition from customer 10 to customer 20
        var res = VehicleDomainRules.TransitionOwnership(1, 10, 20, today);
        Assert.False(res.IsUnchanged);
        Assert.True(res.PreviousOwnerDeactivated);
        Assert.True(res.NewOwnerActivated);
        Assert.Equal(today, res.StartDate);
        Assert.Equal(today, res.TransferDate);

        // Same owner: no transfer
        var sameRes = VehicleDomainRules.TransitionOwnership(1, 10, 10, today, new DateOnly(2025, 1, 1));
        Assert.True(sameRes.IsUnchanged);
        Assert.False(sameRes.PreviousOwnerDeactivated);
        Assert.True(sameRes.NewOwnerActivated);
        Assert.Equal(new DateOnly(2025, 1, 1), sameRes.StartDate);
    }

    [Fact]
    public void VehicleRules_CanAccessVehicle_StaffBypassVsCustomerOwnership()
    {
        var owned = new List<long> { 101, 102 };

        // Staff bypasses
        Assert.True(VehicleDomainRules.CanAccessVehicle(isStaff: true, customerId: null, owned, 999));

        // Customer with matching vehicle
        Assert.True(VehicleDomainRules.CanAccessVehicle(isStaff: false, customerId: 5, owned, 101));

        // Customer with non-owned vehicle
        Assert.False(VehicleDomainRules.CanAccessVehicle(isStaff: false, customerId: 5, owned, 999));

        // Unauthenticated customer
        Assert.False(VehicleDomainRules.CanAccessVehicle(isStaff: false, customerId: null, owned, 101));
    }

    // =========================================================================
    // SECTION 3: Catalog Pure Domain Rules (CatalogDomainRules)
    // =========================================================================

    [Fact]
    public void CatalogRules_StandardServiceCode_StrictlyProhibitsPkgPrefix()
    {
        var (v1, e1) = CatalogDomainRules.ValidateStandardServiceCode("PKG-COMBO-01");
        Assert.False(v1);
        Assert.Contains("Packages", e1);

        var (v2, e2) = CatalogDomainRules.ValidateStandardServiceCode("pkg-oil-change");
        Assert.False(v2);
        Assert.Contains("Packages", e2);

        var (v3, _) = CatalogDomainRules.ValidateStandardServiceCode("SVC-OIL-01");
        Assert.True(v3);

        var (v4, _) = CatalogDomainRules.ValidateStandardServiceCode("");
        Assert.False(v4);
    }

    [Fact]
    public void CatalogRules_PricingAndWarrantyValidation_EnforcesNonNegativity()
    {
        var (p1, _) = CatalogDomainRules.ValidateServicePricing(100_000m, 10m, 20_000m, 30);
        Assert.True(p1);

        var (p2, e2) = CatalogDomainRules.ValidateServicePricing(-100m, 10m, 0m, 30);
        Assert.False(p2);
        Assert.Contains("không được âm", e2);

        var (p3, _) = CatalogDomainRules.ValidateServicePricing(100m, 150m, 0m, 30); // Tax > 100%
        Assert.False(p3);

        var (w1, _) = CatalogDomainRules.ValidateWarranty(12, 10000);
        Assert.True(w1);

        var (w2, _) = CatalogDomainRules.ValidateWarranty(-1, 0);
        Assert.False(w2);

        Assert.Equal(125000.55m, CatalogDomainRules.RoundPrice(125000.548m, 2));
    }

    [Fact]
    public void CatalogRules_CategoryHierarchy_PreventsSelfParenting()
    {
        var (valid, err) = CatalogDomainRules.ValidateCategoryHierarchy(10, 10);
        Assert.False(valid);
        Assert.Contains("chính nó", err);

        var (ok, _) = CatalogDomainRules.ValidateCategoryHierarchy(10, 5);
        Assert.True(ok);
    }

    // =========================================================================
    // SECTION 4: Authorization Services Tests
    // =========================================================================

    [Fact]
    public async Task CustomerAuthorization_EvaluatesPermissionsAccurately()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"CustAuthDb_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Customers.Add(new Customer
        {
            Id = 1,
            FullName = "Nguyễn Văn A",
            Type = "INDIVIDUAL",
            Note = "[PreferredBranchId:2]",
            IsDeleted = false
        });
        db.Customers.Add(new Customer
        {
            Id = 2,
            FullName = "Trần Thị B",
            Type = "INDIVIDUAL",
            Note = null,
            IsDeleted = false
        });
        await db.SaveChangesAsync();

        var auth = new CustomerAuthorizationService(db);

        // Admin can access anyone
        var adminActor = new UserActorContext(999, "Admin", "ADMIN", null, IsAdmin: true);
        Assert.True(await auth.CanAccessCustomerAsync(1, adminActor));
        Assert.True(await auth.CanManageCustomersAsync(adminActor));

        // Staff scoped to branch 2 can access customer 1
        var staffBranch2 = new UserActorContext(100, "Staff", "STAFF", BranchId: 2, IsAdmin: false);
        Assert.True(await auth.CanAccessCustomerAsync(1, staffBranch2));
        Assert.False(await auth.CanAccessCustomerAsync(2, staffBranch2));

        // Customer can only access self
        var customerActor = new UserActorContext(1, "Customer", "CUSTOMER", null, IsAdmin: false);
        Assert.True(await auth.CanAccessCustomerAsync(1, customerActor));
        Assert.False(await auth.CanAccessCustomerAsync(2, customerActor));
    }

    [Fact]
    public async Task VehicleAuthorization_EvaluatesOwnershipAndStaffBypass()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"VehAuthDb_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Vehicles.Add(new Vehicle { Id = 1, LicensePlate = "30A-11111", ModelId = 1, IsDeleted = false });
        db.CustomerVehicles.Add(new CustomerVehicle
        {
            Id = 1,
            CustomerId = 10,
            VehicleId = 1,
            IsCurrentOwner = true,
            IsDeleted = false
        });
        await db.SaveChangesAsync();

        var auth = new VehicleAuthorizationService(db);

        var customer10 = new UserActorContext(10, "Customer", "CUSTOMER", null, IsAdmin: false);
        var customer20 = new UserActorContext(20, "Customer", "CUSTOMER", null, IsAdmin: false);
        var staff = new UserActorContext(100, "Staff", "STAFF", 1, IsAdmin: false);

        // Customer 10 owns vehicle 1
        Assert.True(await auth.CanAccessVehicleAsync(1, customer10));
        Assert.True(await auth.CanModifyVehicleAsync(1, customer10));

        // Customer 20 does not own vehicle 1
        Assert.False(await auth.CanAccessVehicleAsync(1, customer20));
        Assert.False(await auth.CanModifyVehicleAsync(1, customer20));

        // Staff bypasses
        Assert.True(await auth.CanAccessVehicleAsync(1, staff));
        Assert.True(await auth.CanAdministerVehiclesAsync(staff));
    }

    [Fact]
    public async Task CatalogAuthorization_GatingOperatesCorrectly()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"CatAuthDb_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        var auth = new CatalogAuthorizationService(db);

        var anon = new UserActorContext(null, null, null, null, false);
        var staffGeneral = new UserActorContext(10, "Staff", "MECHANIC", 1, false);
        var staffAdmin = new UserActorContext(11, "Staff", "ADMIN", 1, false);
        var admin = new UserActorContext(1, "Admin", "ADMIN", null, true);

        // Public/anon can view catalog
        Assert.True(await auth.CanAccessCatalogItemAsync(anon));

        // Staff list view
        Assert.True(await auth.CanViewCatalogAdminListAsync(staffGeneral));

        // Mutation requires admin role
        Assert.False(await auth.CanManageCatalogAsync(staffGeneral));
        Assert.True(await auth.CanManageCatalogAsync(staffAdmin));
        Assert.True(await auth.CanManageCatalogAsync(admin));
    }

    // =========================================================================
    // SECTION 5: Application Services Operations
    // =========================================================================

    [Fact]
    public async Task CustomerApplicationService_CreateAndRetrieveCustomer_WorksProperly()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"CustAppDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        var auth = new CustomerAuthorizationService(db);
        var service = new CustomerApplicationService(db, auth);
        var staffActor = new UserActorContext(1, "Staff", "ADMIN", 1, true);

        var cmd = new CustomerCreateCommand(
            FullName: "Lê Văn C",
            Phone: "0901234567",
            Email: "levanc@gmail.com",
            Type: "INDIVIDUAL",
            PreferredBranchId: 1
        );

        var created = await service.CreateCustomerAsync(cmd, staffActor);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("Lê Văn C", created.FullName);
        Assert.Contains("[PreferredBranchId:1]", created.Note);

        var detail = await service.GetCustomerByIdAsync(created.Id, staffActor);
        Assert.NotNull(detail);
        Assert.Equal("0901234567", detail.Phone);
    }

    [Fact]
    public async Task VehicleApplicationService_CreateVehicle_AutoGeneratesPlateWhenEmpty()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"VehAppDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "TOYOTA", Name = "Toyota", IsActive = true });
        db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "Camry" });
        await db.SaveChangesAsync();

        var auth = new VehicleAuthorizationService(db);
        var service = new VehicleApplicationService(db, auth);
        var staffActor = new UserActorContext(1, "Staff", "ADMIN", 1, true);

        var cmd = new VehicleUpsertCommand(
            LicensePlate: null, // should trigger TEMP plate
            Vin: "1HGCR2F83HA000001",
            ModelId: 1,
            Color: "Trắng",
            Odo: 15000,
            ManufactureYear: 2022,
            CustomerId: null,
            Note: "Xe mới nhập"
        );

        var result = await service.CreateVehicleAsync(cmd, staffActor);
        Assert.NotNull(result);
        Assert.StartsWith("TEMP-", result.LicensePlate);
        Assert.Equal(15000, result.Odo);
    }

    [Fact]
    public async Task CatalogApplicationService_CreateStandardService_RejectsPkgPrefix()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"CatAppDb_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.ServiceCategories.Add(new ServiceCategory { Id = 1, Code = "MAINT", Name = "Bảo dưỡng", IsActive = true });
        await db.SaveChangesAsync();

        var auth = new CatalogAuthorizationService(db);
        var service = new CatalogApplicationService(db, auth);
        var adminActor = new UserActorContext(1, "Admin", "ADMIN", null, true);

        var pkgCmd = new StandardServiceCreateCommand(
            Code: "PKG-COMBO-01",
            Name: "Combo Bảo dưỡng 10k",
            CategoryId: 1,
            BasePrice: 500_000m
        );

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateStandardServiceAsync(pkgCmd, adminActor));
        Assert.Contains("Packages", ex.Message);
    }

    // =========================================================================
    // SECTION 6: Dual Route API Endpoints Integration Tests (All 17+ Controllers)
    // =========================================================================

    [Fact]
    public async Task DualRoutes_AllModule8Controllers_RespondEquallyOnV1()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            var brand = new VehicleBrand { Id = 1, Code = "TOYOTA", Name = "Toyota", IsActive = true };
            db.VehicleBrands.Add(brand);

            var model = new VehicleModel { Id = 1, BrandId = 1, ModelName = "Vios", Segment = "B", FuelType = "Gasoline" };
            db.VehicleModels.Add(model);

            var customer = new Customer
            {
                Id = 1,
                FullName = "Nguyễn Văn Test",
                Phone = "0912345678",
                Email = "testm8@example.com",
                Type = "INDIVIDUAL",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Customers.Add(customer);

            var vehicle = new Vehicle
            {
                Id = 1,
                LicensePlate = "30A-99999",
                ModelId = 1,
                Color = "Đen",
                Odo = 5000,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };
            db.Vehicles.Add(vehicle);

            db.CustomerVehicles.Add(new CustomerVehicle
            {
                Id = 1,
                CustomerId = 1,
                VehicleId = 1,
                IsCurrentOwner = true,
                OwnershipStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IsDeleted = false
            });

            var cat = new ServiceCategory { Id = 1, Code = "MAINTENANCE", Name = "Bảo dưỡng định kỳ", IsActive = true };
            db.ServiceCategories.Add(cat);

            db.StandardServices.Add(new StandardService
            {
                Id = 1,
                Code = "SVC-OIL-01",
                Name = "Thay dầu máy",
                CategoryId = 1,
                BasePrice = 300_000m,
                StandardMinutes = 30,
                IsActive = true
            });

            db.CustomerTiers.Add(new CustomerTier
            {
                Id = 1,
                TierName = "Standard",
                MinSpending = 0m,
                DiscountPercent = 0m
            });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "ADMIN", 1));

        // 1. CustomerAdministrationController dual routes
        var res1a = await client.GetAsync("/api/customers/admin-list");
        var res1b = await client.GetAsync("/api/v1/customers/admin-list");
        Assert.Equal(res1a.StatusCode, res1b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res1b.StatusCode);

        // 2. CustomersController dual routes
        var res2a = await client.GetAsync("/api/customers");
        var res2b = await client.GetAsync("/api/v1/customers");
        Assert.Equal(res2a.StatusCode, res2b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res2b.StatusCode);

        // 3. CustomerVehiclesController dual routes
        var res3a = await client.GetAsync("/api/customer-vehicles");
        var res3b = await client.GetAsync("/api/v1/customer-vehicles");
        Assert.Equal(res3a.StatusCode, res3b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res3b.StatusCode);

        // 4. CustomerTierController dual routes
        var res4a = await client.GetAsync("/api/customer-tiers");
        var res4b = await client.GetAsync("/api/v1/customer-tiers");
        Assert.Equal(res4a.StatusCode, res4b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res4b.StatusCode);

        // 5. VehiclesController dual routes
        var res5a = await client.GetAsync("/api/vehicles");
        var res5b = await client.GetAsync("/api/v1/vehicles");
        Assert.Equal(res5a.StatusCode, res5b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res5b.StatusCode);

        // 6. VehicleAdminController dual routes
        var res6a = await client.GetAsync("/api/vehicle-admin/lookups");
        var res6b = await client.GetAsync("/api/v1/vehicle-admin/lookups");
        Assert.Equal(res6a.StatusCode, res6b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res6b.StatusCode);

        // 7. VehicleBrandsController dual routes
        var res7a = await client.GetAsync("/api/vehicle-brands");
        var res7b = await client.GetAsync("/api/v1/vehicle-brands");
        Assert.Equal(res7a.StatusCode, res7b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res7b.StatusCode);

        // 8. VehicleBrandAdministrationController dual routes
        var res8a = await client.GetAsync("/api/vehicle-brands/admin-list");
        var res8b = await client.GetAsync("/api/v1/vehicle-brands/admin-list");
        Assert.Equal(res8a.StatusCode, res8b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res8b.StatusCode);

        // 9. VehicleModelsController dual routes
        var res9a = await client.GetAsync("/api/vehicle-models");
        var res9b = await client.GetAsync("/api/v1/vehicle-models");
        Assert.Equal(res9a.StatusCode, res9b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res9b.StatusCode);

        // 10. VehicleModelAdministrationController dual routes
        var res10a = await client.GetAsync("/api/vehicle-models/admin-list");
        var res10b = await client.GetAsync("/api/v1/vehicle-models/admin-list");
        Assert.Equal(res10a.StatusCode, res10b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res10b.StatusCode);

        // 11. StandardServicesController dual routes
        var res11a = await client.GetAsync("/api/standard-services");
        var res11b = await client.GetAsync("/api/v1/standard-services");
        Assert.Equal(res11a.StatusCode, res11b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res11b.StatusCode);

        // 12. StandardServiceAdministrationController dual routes
        var res12a = await client.GetAsync("/api/standard-services/admin-list");
        var res12b = await client.GetAsync("/api/v1/standard-services/admin-list");
        Assert.Equal(res12a.StatusCode, res12b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res12b.StatusCode);

        // 13. ServiceCategoriesController dual routes
        var res13a = await client.GetAsync("/api/service-categories");
        var res13b = await client.GetAsync("/api/v1/service-categories");
        Assert.Equal(res13a.StatusCode, res13b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res13b.StatusCode);

        // 14. ServiceCategoryAdministrationController dual routes
        var res14a = await client.GetAsync("/api/service-categories/admin-list");
        var res14b = await client.GetAsync("/api/v1/service-categories/admin-list");
        Assert.Equal(res14a.StatusCode, res14b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res14b.StatusCode);

        // 15. ServiceBomsController dual routes
        var res15a = await client.GetAsync("/api/service-boms?serviceId=1&vehicleModelId=1");
        var res15b = await client.GetAsync("/api/v1/service-boms?serviceId=1&vehicleModelId=1");
        Assert.Equal(res15a.StatusCode, res15b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res15b.StatusCode);

        // 16. ModelSpecificRuleController dual routes
        var res16a = await client.GetAsync("/api/model-specific-rules");
        var res16b = await client.GetAsync("/api/v1/model-specific-rules");
        Assert.Equal(res16a.StatusCode, res16b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res16b.StatusCode);

        // 17. CustomersMeController dual routes (as Customer)
        using var custClient = factory.CreateClient();
        custClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer", "CUSTOMER", null));
        var res17a = await custClient.GetAsync("/api/customers/me/vehicles");
        var res17b = await custClient.GetAsync("/api/v1/customers/me/vehicles");
        Assert.Equal(res17a.StatusCode, res17b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res17b.StatusCode);

        // 18. CustomerFeedbackController dual routes
        var res18a = await client.GetAsync("/api/customer-feedbacks");
        var res18b = await client.GetAsync("/api/v1/customer-feedbacks");
        Assert.Equal(res18a.StatusCode, res18b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res18b.StatusCode);

        // 19. VehicleInsuranceController dual routes
        var res19a = await client.GetAsync("/api/vehicle-insurances");
        var res19b = await client.GetAsync("/api/v1/vehicle-insurances");
        Assert.Equal(res19a.StatusCode, res19b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res19b.StatusCode);

        // 20. VehicleConditionLogController dual routes
        var res20a = await client.GetAsync("/api/vehicle-condition-logs");
        var res20b = await client.GetAsync("/api/v1/vehicle-condition-logs");
        Assert.Equal(res20a.StatusCode, res20b.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res20b.StatusCode);
    }
}
