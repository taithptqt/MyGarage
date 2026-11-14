using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class VoucherScreenPaginationTests
{
    [Fact]
    public async Task CampaignDetail_PagesOnlyItsActiveVouchersAndKeepsLegacyDetail()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VoucherCampaigns.Add(new VoucherCampaign { Id = 1, Name = "Campaign", Status = "ACTIVE" });
            for (var i = 1; i <= 13; i++)
                db.Vouchers.Add(new Voucher { Id = i, Code = $"V{i:D2}", VoucherName = "Voucher", Type = "FIXED",
                    Status = "ACTIVE", Timezone = "Asia/Ho_Chi_Minh", Version = Array.Empty<byte>(),
                    CampaignId = i == 13 ? null : 1, IsDeleted = i == 12 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        using var summary = JsonDocument.Parse(await client.GetStringAsync("/api/voucher-campaigns/1/summary"));
        Assert.Equal(11, summary.RootElement.GetProperty("voucherCount").GetInt32());
        Assert.Empty(summary.RootElement.GetProperty("vouchers").EnumerateArray());
        using var page = JsonDocument.Parse(await client.GetStringAsync("/api/voucher-campaigns/1/voucher-page?page=2&pageSize=5"));
        Assert.Equal(11, page.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(6, page.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/voucher-campaigns/999/voucher-page")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/voucher-campaigns/1/summary")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_RequiresAdminAndUsesGlobalTotalsAndBoundedRankings()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 10; i++)
                db.Vouchers.Add(new Voucher { Id = i, Code = $"V{i}", VoucherName = $"Voucher {i}", Type = "FIXED",
                    Status = i == 1 ? "PAUSED" : "ACTIVE", IsActive = i == 1, Timezone = "Asia/Ho_Chi_Minh",
                    Version = Array.Empty<byte>(), IssuedCount = i == 1 ? 0 : 10, RedeemedCount = i,
                    BudgetLimit = 100m, BudgetUsed = i, IsDeleted = i == 10 });
            db.VoucherRedemptions.Add(new VoucherRedemption { Id = 21, VoucherId = 1, VoucherCode = "V1",
                BranchId = 2, ValidationStatus = "VALID", RedemptionStatus = "REDEEMED", DiscountAmount = 35m });
            for (var i = 1; i <= 8; i++)
                db.VoucherApprovalRequests.Add(new VoucherApprovalRequest { Id = i, RequestType = "EXCEPTION", Reason = "reason",
                    Status = "PENDING", RiskLevel = "HIGH", RequestedBy = 200, CreatedAt = new DateTime(2026, 1, 1).AddMinutes(i) });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/vouchers/dashboard")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var response = await client.GetAsync("/api/vouchers/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(10, json.RootElement.GetProperty("activeCount").GetInt32());
        Assert.Equal(90, json.RootElement.GetProperty("issued").GetInt64());
        Assert.Equal(55, json.RootElement.GetProperty("redeemed").GetInt64());
        Assert.Equal(35, json.RootElement.GetProperty("totalDiscount").GetDecimal());
        Assert.Equal(8, json.RootElement.GetProperty("pendingApprovals").GetInt32());
        Assert.Equal(6, json.RootElement.GetProperty("approvalItems").GetArrayLength());
        Assert.Equal(8, json.RootElement.GetProperty("topVouchers").GetArrayLength());
        Assert.Equal(10, json.RootElement.GetProperty("topVouchers")[0].GetProperty("id").GetInt64());
        Assert.Equal(3, json.RootElement.GetProperty("nearQuotaVouchers").GetArrayLength());
        Assert.Equal(10, json.RootElement.GetProperty("nearQuotaVouchers")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task CampaignPage_SummarizesAllCampaignsButFiltersBeforePaging()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 12; i++)
            {
                db.VoucherCampaigns.Add(new VoucherCampaign { Id = i, CampaignCode = $"C{i}", Name = "Campaign",
                    Description = "Description", Objective = "Objective", Status = i == 1 ? "PAUSED" : "ACTIVE",
                    BudgetLimit = 100m, BudgetUsed = i == 1 ? 90m : 0m,
                    CreatedAt = new DateTime(2026, 1, 1), UpdatedAt = new DateTime(2026, 1, 1).AddMinutes(i) });
                db.Vouchers.Add(new Voucher { Id = i, CampaignId = i, Code = $"V{i}", VoucherName = $"Voucher {i}",
                    Type = "FIXED", Status = "ACTIVE", IsActive = true, Timezone = "Asia/Ho_Chi_Minh",
                    Version = Array.Empty<byte>(), RedeemedCount = i, IsDeleted = i == 1 });
            }
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/voucher-campaigns/admin-page?search=Campaign&status=ACTIVE&page=2&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(11, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(7, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        var stats = json.RootElement.GetProperty("stats");
        Assert.Equal(12, stats.GetProperty("totalCampaigns").GetInt32());
        Assert.Equal(11, stats.GetProperty("activeCampaigns").GetInt32());
        Assert.Equal(78, stats.GetProperty("totalRedeemed").GetInt64());
        Assert.Equal(1, stats.GetProperty("budgetWarningCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("budgetWarnings")[0].GetProperty("id").GetInt64());
        var deletedCampaignVoucher = await client.GetAsync("/api/voucher-campaigns/admin-page?search=Campaign%20C1%20Description%20Objective");
        using var filtered = JsonDocument.Parse(await deletedCampaignVoucher.Content.ReadAsStringAsync());
        Assert.Equal(1, filtered.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(0, filtered.RootElement.GetProperty("items")[0].GetProperty("voucherCount").GetInt32());
        Assert.Equal(1, filtered.RootElement.GetProperty("items")[0].GetProperty("redeemed").GetInt64());
    }

    [Fact]
    public async Task VoucherReport_ComputesTotalsAcrossAllPagesAndOrdersByRedeemed()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 12; i++)
                db.Vouchers.Add(new Voucher { Id = i, Code = $"V{i}", VoucherName = $"Voucher {i}", Type = "FIXED",
                    Status = "ACTIVE", Timezone = "Asia/Ho_Chi_Minh", Version = Array.Empty<byte>(),
                    IssuedCount = i * 10, RedeemedCount = i, BudgetLimit = 100m, BudgetUsed = i });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/vouchers/report-page?page=2&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(12, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(780, json.RootElement.GetProperty("issued").GetInt64());
        Assert.Equal(78, json.RootElement.GetProperty("redeemed").GetInt64());
        Assert.Equal(1200, json.RootElement.GetProperty("budgetLimit").GetDecimal());
        Assert.Equal(78, json.RootElement.GetProperty("budgetUsed").GetDecimal());
        Assert.Equal(7, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task RedemptionPage_FiltersBeforePaging_OrdersReportAndRestrictsBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 14; i++)
                db.VoucherRedemptions.Add(new VoucherRedemption { Id = i, VoucherId = 1,
                    VoucherCode = "SALE", CustomerId = i == 1 ? 123 : 100, BranchId = i <= 12 ? 1 : i == 13 ? 2 : null,
                    ValidationStatus = "VALID", RedemptionStatus = "REDEEMED", DiscountAmount = i,
                    AppliedAt = new DateTime(2026, 1, 1), CreatedAt = new DateTime(2026, 1, 1).AddMinutes(i) });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/voucher-redemptions/admin-page?search=SALE&page=2&pageSize=5&sort=discountDesc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(12, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(7, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        var crossField = await client.GetAsync("/api/voucher-redemptions/admin-page?search=SALE%201%20123");
        using var crossJson = JsonDocument.Parse(await crossField.Content.ReadAsStringAsync());
        Assert.Equal(1, crossJson.RootElement.GetProperty("total").GetInt32());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var admin = await client.GetAsync("/api/voucher-redemptions/admin-page?pageSize=20");
        using var adminJson = JsonDocument.Parse(await admin.Content.ReadAsStringAsync());
        Assert.Equal(14, adminJson.RootElement.GetProperty("total").GetInt32());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/voucher-redemptions/admin-page")).StatusCode);
    }

    [Fact]
    public async Task ApprovalPage_RequiresAdminAndFiltersBeforePaging()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 12; i++)
                db.VoucherApprovalRequests.Add(new VoucherApprovalRequest { Id = i, VoucherId = 9,
                    RequestType = "EXCEPTION", Reason = "reason", Status = "PENDING", RiskLevel = "HIGH",
                    RequestedBy = 200, CreatedAt = new DateTime(2026, 1, 1).AddMinutes(i) });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var response = await client.GetAsync("/api/voucher-approval-requests/admin-page?search=EXCEPTION&page=2&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(12, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(7, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        var crossField = await client.GetAsync("/api/voucher-approval-requests/admin-page?search=reason%201%209");
        using var crossJson = JsonDocument.Parse(await crossField.Content.ReadAsStringAsync());
        Assert.Equal(1, crossJson.RootElement.GetProperty("total").GetInt32());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/voucher-approval-requests/admin-page")).StatusCode);
    }
}
