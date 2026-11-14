using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MyGarage.Domain.Pricing;
using MyGarage.DTOs.Requests;
using MyGarage.Middleware;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class QuotationModuleTests
{
    [Fact]
    public void QuotationPricingEngine_CalculatesAccurately_AndRespectsRelationalDiscounts()
    {
        // Arrange: Detail 1 (10% tax, line discount 100_000)
        var detail1 = new DetailPricingInput(
            Id: 1,
            Quantity: 2,
            UnitPriceAtTime: 500_000m,
            TaxRate: 10m,
            LineDiscounts: 100_000m,
            InsuranceCoveredAmount: null,
            CustomerCoveredAmount: null,
            PayerType: "CUSTOMER",
            Status: "ACTIVE"
        );

        // Detail 2 (8% tax, no line discount, insurance covered 1_000_000)
        var detail2 = new DetailPricingInput(
            Id: 2,
            Quantity: 1,
            UnitPriceAtTime: 2_000_000m,
            TaxRate: 8m,
            LineDiscounts: 0m,
            InsuranceCoveredAmount: 1_000_000m,
            CustomerCoveredAmount: null,
            PayerType: null,
            Status: "ACTIVE"
        );

        // Detail 3 (Canceled - should be ignored in totals)
        var detail3 = new DetailPricingInput(
            Id: 3,
            Quantity: 1,
            UnitPriceAtTime: 1_000_000m,
            TaxRate: 10m,
            LineDiscounts: 0m,
            InsuranceCoveredAmount: null,
            CustomerCoveredAmount: null,
            PayerType: "CUSTOMER",
            Status: "CANCELED"
        );

        var quotationLevelDiscount = 200_000m;

        // Act
        var result = QuotationPricingEngine.CalculateTotals([detail1, detail2, detail3], quotationLevelDiscount);

        // Assert Detail 1:
        // SubTotal = 2 * 500_000 = 1_000_000
        // Discount = 100_000
        // TaxableBase = 900_000
        // TaxAmount = 900_000 * 10% = 90_000
        // FinalAmount = 990_000
        var d1 = result.Details[0];
        Assert.Equal(1_000_000m, d1.SubTotal);
        Assert.Equal(100_000m, d1.LineDiscount);
        Assert.Equal(900_000m, d1.TaxableBase);
        Assert.Equal(90_000m, d1.TaxAmount);
        Assert.Equal(990_000m, d1.FinalAmount);
        Assert.Equal("CUSTOMER", d1.PayerType);
        Assert.Equal(0m, d1.InsuranceCoveredAmount);
        Assert.Equal(990_000m, d1.CustomerCoveredAmount);

        // Assert Detail 2:
        // SubTotal = 2_000_000
        // Discount = 0
        // TaxableBase = 2_000_000
        // TaxAmount = 2_000_000 * 8% = 160_000
        // FinalAmount = 2_160_000
        // Insurance = 1_000_000, Customer = 1_160_000 -> SPLIT
        var d2 = result.Details[1];
        Assert.Equal(2_000_000m, d2.SubTotal);
        Assert.Equal(0m, d2.LineDiscount);
        Assert.Equal(2_000_000m, d2.TaxableBase);
        Assert.Equal(160_000m, d2.TaxAmount);
        Assert.Equal(2_160_000m, d2.FinalAmount);
        Assert.Equal("SPLIT", d2.PayerType);
        Assert.Equal(1_000_000m, d2.InsuranceCoveredAmount);
        Assert.Equal(1_160_000m, d2.CustomerCoveredAmount);

        // Assert Totals:
        // SubTotalAmount = 1_000_000 + 2_000_000 = 3_000_000
        // TaxAmount = 90_000 + 160_000 = 250_000
        // DetailsFinal = 990_000 + 2_160_000 = 3_150_000
        // FinalAmount = 3_150_000 - 200_000 = 2_950_000
        // TotalInsuranceAmount = 0 + 1_000_000 = 1_000_000
        // TotalCustomerAmount = (990_000 + 1_160_000) - 200_000 = 1_950_000
        Assert.Equal(3_000_000m, result.SubTotalAmount);
        Assert.Equal(250_000m, result.TaxAmount);
        Assert.Equal(2_950_000m, result.FinalAmount);
        Assert.Equal(1_000_000m, result.TotalInsuranceAmount);
        Assert.Equal(1_950_000m, result.TotalCustomerAmount);
        Assert.Equal(2, result.Details.Count); // Canceled detail excluded
    }

    [Theory]
    [InlineData("/api/quotations/public/0123456789abcdef", "/api/quotations/public/[REDACTED]")]
    [InlineData("/api/quotations/public/secret-token-xyz/decision", "/api/quotations/public/[REDACTED]/decision")]
    [InlineData("/api/quotations/public/secret-token-xyz/otp/send", "/api/quotations/public/[REDACTED]/otp/send")]
    [InlineData("/api/quotations/123", "/api/quotations/123")]
    [InlineData("/api/service-orders/500/quotations", "/api/service-orders/500/quotations")]
    public void GlobalExceptionHandler_SanitizesPublicQuotationTokenPath(string rawPath, string expectedSanitized)
    {
        var sanitized = GlobalExceptionHandler.SanitizePathForLogging(rawPath);
        Assert.Equal(expectedSanitized, sanitized);
    }

    [Fact]
    public async Task QuotationDiscounts_SecurityScoping_EnforcesOwnershipAndBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            // Add second customer
            db.Customers.Add(new Customer
            {
                Id = 101,
                FullName = "Trần Thị Khách 2",
                Phone = "0909998877",
                Email = "customer101@example.com",
                PasswordHash = "hash",
                Type = "INDIVIDUAL",
                SecurityStamp = "test-stamp",
                IsActive = true
            });

            // Staff at Branch 1 (Advisor)
            db.Staffs.Add(new Staff
            {
                Id = 205,
                FullName = "Cố Vấn CN1",
                Phone = "0901234567",
                Email = "advisor1@example.com",
                PasswordHash = "hash",
                BranchId = 1,
                Status = "ACTIVE",
                SecurityStamp = "test-stamp"
            });

            // Staff at Branch 2 (Advisor)
            db.Staffs.Add(new Staff
            {
                Id = 206,
                FullName = "Cố Vấn CN2",
                Phone = "0907654321",
                Email = "advisor2@example.com",
                PasswordHash = "hash",
                BranchId = 2,
                Status = "ACTIVE",
                SecurityStamp = "test-stamp"
            });

            // ServiceOrder in Branch 1 for Customer 100
            var so = new ServiceOrder
            {
                Id = 1000,
                Code = "SO-TEST-1000",
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                CustomerId = 100,
                VehicleId = 1,
                Status = "DIAGNOSING"
            };
            db.ServiceOrders.Add(so);

            // Quotation 500 for ServiceOrder 1000
            var quotation = new Quotation
            {
                Id = 500,
                Code = "QT-TEST-500",
                ServiceOrderId = 1000,
                VersionNo = 1,
                QuotationType = "REPAIR",
                InsuranceChangeType = "NONE",
                Status = "DRAFT",
                SubTotalAmount = 1_000_000m,
                FinalAmount = 900_000m,
                QuotationDiscounts =
                {
                    new QuotationDiscount
                    {
                        Id = 50,
                        QuotationId = 500,
                        DiscountType = "MANUAL",
                        ValueType = "AMOUNT",
                        DiscountValue = 100_000m,
                        AppliedAmount = 100_000m
                    }
                }
            };
            db.Quotations.Add(quotation);

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();

        // 1. Anonymous request -> 401
        var anonRes = await client.GetAsync("/api/quotations/500/discounts");
        Assert.Equal(HttpStatusCode.Unauthorized, anonRes.StatusCode);

        // 2. Customer 100 (Owner) -> 200 OK
        var customer100Token = TestWebApplicationFactory.CreateJwtToken(100, "Customer");
        using var clientCust100 = factory.CreateClient();
        clientCust100.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customer100Token);
        var cust100Res = await clientCust100.GetAsync("/api/quotations/500/discounts");
        Assert.Equal(HttpStatusCode.OK, cust100Res.StatusCode);

        // 3. Customer 101 (Non-owner) -> 403 Forbidden
        var customer101Token = TestWebApplicationFactory.CreateJwtToken(101, "Customer");
        using var clientCust101 = factory.CreateClient();
        clientCust101.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customer101Token);
        var cust101Res = await clientCust101.GetAsync("/api/quotations/500/discounts");
        Assert.Equal(HttpStatusCode.Forbidden, cust101Res.StatusCode);

        // 4. Staff at Branch 1 (Same branch) -> 200 OK
        var staffBranch1Token = TestWebApplicationFactory.CreateJwtToken(205, "Staff", "SERVICE_ADVISOR", branchId: 1);
        using var clientStaffB1 = factory.CreateClient();
        clientStaffB1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffBranch1Token);
        var staffB1Res = await clientStaffB1.GetAsync("/api/quotations/500/discounts");
        Assert.Equal(HttpStatusCode.OK, staffB1Res.StatusCode);

        // 5. Staff at Branch 2 (Different branch) -> 403 Forbidden
        var staffBranch2Token = TestWebApplicationFactory.CreateJwtToken(206, "Staff", "SERVICE_ADVISOR", branchId: 2);
        using var clientStaffB2 = factory.CreateClient();
        clientStaffB2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffBranch2Token);
        var staffB2Res = await clientStaffB2.GetAsync("/api/quotations/500/discounts");
        Assert.Equal(HttpStatusCode.Forbidden, staffB2Res.StatusCode);

        // 6. Admin Staff -> 200 OK
        var adminToken = TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", branchId: 1);
        using var clientAdmin = factory.CreateClient();
        clientAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var adminRes = await clientAdmin.GetAsync("/api/quotations/500/discounts");
        Assert.Equal(HttpStatusCode.OK, adminRes.StatusCode);
    }

    [Fact]
    public async Task QuotationDetail_SecurityScoping_EnforcesOwnershipAndBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.Customers.Add(new Customer
            {
                Id = 101,
                FullName = "Khách Hàng Khác",
                Phone = "0908887766",
                Email = "other@example.com",
                PasswordHash = "hash",
                Type = "INDIVIDUAL",
                SecurityStamp = "test-stamp",
                IsActive = true
            });

            db.Staffs.Add(new Staff
            {
                Id = 207,
                FullName = "Staff Chi Nhánh 2",
                Phone = "0901112222",
                Email = "staff2@example.com",
                PasswordHash = "hash",
                BranchId = 2,
                Status = "ACTIVE",
                SecurityStamp = "test-stamp"
            });

            var so = new ServiceOrder
            {
                Id = 2000,
                Code = "SO-TEST-2000",
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                CustomerId = 100,
                VehicleId = 1,
                Status = "DIAGNOSING"
            };
            db.ServiceOrders.Add(so);

            var quotation = new Quotation
            {
                Id = 600,
                Code = "QT-TEST-600",
                ServiceOrderId = 2000,
                VersionNo = 1,
                QuotationType = "REPAIR",
                InsuranceChangeType = "NONE",
                Status = "DRAFT",
                SubTotalAmount = 500_000m,
                FinalAmount = 500_000m
            };
            db.Quotations.Add(quotation);

            return Task.CompletedTask;
        });

        // 1. Owner customer (100) -> 200 OK
        using var clientCust100 = factory.CreateClient();
        clientCust100.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        var res1 = await clientCust100.GetAsync("/api/quotations/600");
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // 2. Non-owner customer (101) -> 403 Forbidden
        using var clientCust101 = factory.CreateClient();
        clientCust101.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(101, "Customer"));
        var res2 = await clientCust101.GetAsync("/api/quotations/600");
        Assert.Equal(HttpStatusCode.Forbidden, res2.StatusCode);

        // 3. Staff at Branch 1 -> 200 OK
        using var clientStaffB1 = factory.CreateClient();
        clientStaffB1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "SERVICE_ADVISOR", branchId: 1));
        var res3 = await clientStaffB1.GetAsync("/api/quotations/600");
        Assert.Equal(HttpStatusCode.OK, res3.StatusCode);

        // 4. Staff at Branch 2 -> 403 Forbidden
        using var clientStaffB2 = factory.CreateClient();
        clientStaffB2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(207, "Staff", "SERVICE_ADVISOR", branchId: 2));
        var res4 = await clientStaffB2.GetAsync("/api/quotations/600");
        Assert.Equal(HttpStatusCode.Forbidden, res4.StatusCode);
    }

    [Fact]
    public async Task UpsertQuotationDiscounts_EnforcesBranchAndSpoofingPrevention()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.Staffs.Add(new Staff
            {
                Id = 208,
                FullName = "Cố Vấn CN1 Thật",
                Phone = "0901113333",
                Email = "advisor_real@example.com",
                PasswordHash = "hash",
                BranchId = 1,
                Status = "ACTIVE",
                SecurityStamp = "test-stamp"
            });

            db.Staffs.Add(new Staff
            {
                Id = 209,
                FullName = "Cố Vấn CN2",
                Phone = "0901114444",
                Email = "advisor_cn2@example.com",
                PasswordHash = "hash",
                BranchId = 2,
                Status = "ACTIVE",
                SecurityStamp = "test-stamp"
            });

            var so = new ServiceOrder
            {
                Id = 3000,
                Code = "SO-TEST-3000",
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                CustomerId = 100,
                VehicleId = 1,
                Status = "DIAGNOSING"
            };
            db.ServiceOrders.Add(so);

            var quotation = new Quotation
            {
                Id = 700,
                Code = "QT-TEST-700",
                ServiceOrderId = 3000,
                VersionNo = 1,
                QuotationType = "REPAIR",
                InsuranceChangeType = "NONE",
                Status = "DRAFT",
                SubTotalAmount = 500_000m,
                FinalAmount = 500_000m
            };
            db.Quotations.Add(quotation);

            return Task.CompletedTask;
        });

        // 1. Customer attempting discount update -> 403 Forbidden
        using var clientCust = factory.CreateClient();
        clientCust.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        var custRes = await clientCust.PutAsJsonAsync("/api/quotations/700/discounts", new QuotationDiscountUpsertRequestDto
        {
            ActorStaffId = 100,
            Items = new List<QuotationDiscountUpsertItemDto>()
        });
        Assert.Equal(HttpStatusCode.Forbidden, custRes.StatusCode);

        // 2. Staff from Branch 2 attempting discount update for Branch 1 -> 403 Forbidden
        using var clientStaffB2 = factory.CreateClient();
        clientStaffB2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(209, "Staff", "SERVICE_ADVISOR", branchId: 2));
        var staffB2Res = await clientStaffB2.PutAsJsonAsync("/api/quotations/700/discounts", new QuotationDiscountUpsertRequestDto
        {
            ActorStaffId = 209,
            Items = new List<QuotationDiscountUpsertItemDto>()
        });
        Assert.Equal(HttpStatusCode.Forbidden, staffB2Res.StatusCode);

        // 3. Staff 208 from Branch 1 attempting to spoof ActorStaffId = 999 -> 403 Forbidden
        using var clientSpoof = factory.CreateClient();
        clientSpoof.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(208, "Staff", "SERVICE_ADVISOR", branchId: 1));
        var spoofRes = await clientSpoof.PutAsJsonAsync("/api/quotations/700/discounts", new QuotationDiscountUpsertRequestDto
        {
            ActorStaffId = 999, // Mismatched staff ID!
            Items = new List<QuotationDiscountUpsertItemDto>()
        });
        Assert.Equal(HttpStatusCode.Forbidden, spoofRes.StatusCode);
    }
}
