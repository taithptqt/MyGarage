using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class VoucherRedemptionLegacyScopeTests
{
    [Fact]
    public async Task InvoiceVoucherValidation_UsesInvoiceAmountInsteadOfClientAmount()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Vouchers.Add(new Voucher { Id = 9, Code = "MINIMUM", VoucherName = "Minimum", Type = "FIXED", Value = 10,
                MinOrderValue = 500, Status = "ACTIVE", IsActive = true, StartDate = new DateTime(2020, 1, 1),
                EndDate = new DateTime(2100, 1, 1), Timezone = "Asia/Ho_Chi_Minh", Version = Array.Empty<byte>() });
            db.RetailOrders.Add(new RetailOrder { Id = 81, BranchId = 1, OrderCode = "R-81" });
            db.Invoices.Add(new Invoice { Id = 91, RetailOrderId = 81, PayerType = "CUSTOMER", TotalAmount = 100 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.PostAsJsonAsync("/api/voucher-redemptions/validations", new { VoucherId = 9, InvoiceId = 91, EligibleAmount = 1000 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VCH_MIN_ORDER_NOT_MET", json.RootElement.GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task Mutations_EnforceBranch_Identity_ApprovalAndIdempotencyScope()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Vouchers.Add(new Voucher { Id = 1, Code = "SALE", VoucherName = "Sale", Type = "FIXED", Status = "ACTIVE", Timezone = "Asia/Ho_Chi_Minh", Version = Array.Empty<byte>() });
            db.RetailOrders.Add(new RetailOrder { Id = 80, BranchId = 2, OrderCode = "R-80" });
            db.RetailOrders.Add(new RetailOrder { Id = 81, BranchId = 1, OrderCode = "R-81", CustomerId = 100 });
            db.Invoices.Add(new Invoice { Id = 90, RetailOrderId = 80, PayerType = "CUSTOMER" });
            db.Invoices.Add(new Invoice { Id = 91, RetailOrderId = 81, PayerType = "CUSTOMER" });
            db.VoucherRedemptions.Add(new VoucherRedemption { Id = 1, VoucherId = 1, VoucherCode = "SALE", BranchId = 1, AppliedBy = 200, CustomerId = 100, IdempotencyKey = "same-key", ValidationStatus = "VALID", RedemptionStatus = "REDEEMED", CreatedAt = new DateTime(2026, 1, 1), AppliedAt = new DateTime(2026, 1, 1) });
            db.VoucherRedemptions.Add(new VoucherRedemption { Id = 2, VoucherId = 1, VoucherCode = "SALE", BranchId = 2, ValidationStatus = "VALID", RedemptionStatus = "REDEEMED", CreatedAt = new DateTime(2026, 1, 1), AppliedAt = new DateTime(2026, 1, 1) });
            db.VoucherApprovalRequests.Add(new VoucherApprovalRequest { Id = 10, RedemptionId = 1, BranchId = 1, Status = "PENDING", RequestType = "EXCEPTION", RiskLevel = "HIGH", Reason = "test", RequestedBy = 200 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/voucher-redemptions/validations", new { BranchId = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/voucher-redemptions/transactions", new { RetailOrderId = 80 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/voucher-redemptions/transactions", new { InvoiceId = 90 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/voucher-redemptions/validations", new { RetailOrderId = 81, CustomerId = 999 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/voucher-redemptions/validations", new { InvoiceId = 91, CustomerId = 999 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/voucher-redemptions/transactions/2/reversals", new { Reason = "test" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/voucher-redemptions/transactions/1/reversals", new { ApprovalRequestId = 10, Reason = "test" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/voucher-redemptions/transactions", new { VoucherId = 1, CustomerId = 999, IdempotencyKey = "same-key", AppliedBy = 999 })).StatusCode);
        var replay = await client.PostAsJsonAsync("/api/voucher-redemptions/transactions", new { VoucherId = 1, CustomerId = 100, IdempotencyKey = "same-key", AppliedBy = 999 });
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(200, replayJson.RootElement.GetProperty("redemption").GetProperty("appliedBy").GetInt64());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/voucher-redemptions/validations", new { RetailOrderId = 81, InvoiceId = 90 })).StatusCode);
    }

    [Fact]
    public async Task LegacyRoutes_ScopeBeforePagination_AndDenyCrossBranchOrMissingBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Vouchers.Add(new Voucher { Id = 1, Code = "SALE", VoucherName = "Sale", Type = "FIXED", Status = "ACTIVE", Timezone = "Asia/Ho_Chi_Minh", Version = Array.Empty<byte>() });
            db.VoucherRedemptions.AddRange(
                new VoucherRedemption { Id = 1, VoucherId = 1, VoucherCode = "SALE", BranchId = 1, ValidationStatus = "VALID", RedemptionStatus = "REDEEMED", CreatedAt = new DateTime(2026, 1, 1), AppliedAt = new DateTime(2026, 1, 1) },
                new VoucherRedemption { Id = 2, VoucherId = 1, VoucherCode = "SALE", BranchId = 2, ValidationStatus = "VALID", RedemptionStatus = "REDEEMED", CreatedAt = new DateTime(2026, 1, 2), AppliedAt = new DateTime(2026, 1, 2) },
                new VoucherRedemption { Id = 3, VoucherId = 1, VoucherCode = "SALE", BranchId = null, ValidationStatus = "VALID", RedemptionStatus = "REDEEMED", CreatedAt = new DateTime(2026, 1, 3), AppliedAt = new DateTime(2026, 1, 3) });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var list = await client.GetAsync("/api/voucher-redemptions?status=redeemed&page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.False(list.Headers.Contains("X-Total-Count"));
        using var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(1, body.RootElement.GetProperty("totalCount").GetInt32());
        var items = body.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(1, items[0].GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/voucher-redemptions/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/voucher-redemptions/2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/voucher-redemptions/3")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/voucher-redemptions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/voucher-redemptions/1")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN"));
        var adminList = await client.GetAsync("/api/voucher-redemptions?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, adminList.StatusCode);
        Assert.False(adminList.Headers.Contains("X-Total-Count"));
        using var adminBody = JsonDocument.Parse(await adminList.Content.ReadAsStringAsync());
        Assert.Equal(3, adminBody.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/voucher-redemptions/2")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/voucher-redemptions/3")).StatusCode);
    }
}
