using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.Hubs;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.WorkflowTests;

public sealed class QuotationWorkflowApprovalTests
{
    [Fact]
    public async Task ApproveQuotationAsync_WhenWithinThreshold_ShouldNotRequireFinancialApproval()
    {
        await using var context = CreateContext(nameof(ApproveQuotationAsync_WhenWithinThreshold_ShouldNotRequireFinancialApproval));
        await SeedApprovalCaseAsync(
            context,
            unitPrice: 1_000_000m,
            discountValue: 10_000m,
            quotationStatus: "WAITING_APPROVAL");

        var sut = CreateService(context);
        var response = await sut.ApproveQuotationAsync(1, new QuotationApproveRequestDto
        {
            ApprovedBy = 200,
            Note = "Duyệt kỹ thuật"
        });

        Assert.Equal("APPROVED", response.TechnicalApprovalStatus);
        Assert.False(response.RequiresFinancialApproval);
        Assert.Equal("NOT_REQUIRED", response.FinancialApprovalStatus);
        Assert.Equal("PENDING_INSURANCE", response.Status);

        var action = await context.QuotationApprovalActions.SingleAsync();
        Assert.Equal("TECHNICAL", action.ApprovalType);
        Assert.Equal("APPROVED", action.Decision);
    }

    [Fact]
    public async Task ApproveQuotationAsync_WhenDiscountExceedsThreshold_ShouldWaitBranchManagerApproval()
    {
        await using var context = CreateContext(nameof(ApproveQuotationAsync_WhenDiscountExceedsThreshold_ShouldWaitBranchManagerApproval));
        await SeedApprovalCaseAsync(
            context,
            unitPrice: 1_000_000m,
            discountValue: 100_000m,
            quotationStatus: "WAITING_APPROVAL");

        var sut = CreateService(context);
        var response = await sut.ApproveQuotationAsync(1, new QuotationApproveRequestDto
        {
            ApprovedBy = 200,
            Note = "Duyệt kỹ thuật"
        });

        Assert.Equal("APPROVED", response.TechnicalApprovalStatus);
        Assert.True(response.RequiresFinancialApproval);
        Assert.Equal("PENDING", response.FinancialApprovalStatus);
        Assert.Equal("PENDING_BRANCH_MANAGER", response.Status);
    }

    [Fact]
    public async Task FinancialApproveQuotationAsync_WhenBranchManagerRejects_ShouldSetRejectedStatus()
    {
        await using var context = CreateContext(nameof(FinancialApproveQuotationAsync_WhenBranchManagerRejects_ShouldSetRejectedStatus));
        await SeedPendingFinancialApprovalCaseAsync(context);

        var sut = CreateService(context);
        var response = await sut.FinancialApproveQuotationAsync(1, new QuotationFinancialApprovalRequestDto
        {
            ApprovedBy = 300,
            Approved = false,
            Note = "Vượt khung chiết khấu"
        });

        Assert.Equal("REJECTED", response.Status);
        Assert.Equal("REJECTED", response.FinancialApprovalStatus);

        var quotation = await context.Quotations.AsNoTracking().SingleAsync(x => x.Id == 1);
        Assert.Equal("FINANCIAL_REJECTED", quotation.RejectReason);

        var action = await context.QuotationApprovalActions.SingleAsync();
        Assert.Equal("FINANCIAL", action.ApprovalType);
        Assert.Equal("REJECTED", action.Decision);
        Assert.Equal(300, action.ActorStaffId);
    }

    private static GarageContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new GarageContext(options, new HttpContextAccessor());
    }

    private static QuotationWorkflowService CreateService(GarageContext context)
    {
        var serviceOrderDetailService = new Mock<IServiceOrderDetailService>(MockBehavior.Loose);
        var emailService = new Mock<IEmailService>(MockBehavior.Loose);
        var hubContext = new Mock<IHubContext<ChatHub>>(MockBehavior.Loose);
        var configuration = new ConfigurationBuilder().Build();

        return new QuotationWorkflowService(
            context,
            serviceOrderDetailService.Object,
            emailService.Object,
            configuration,
            hubContext.Object);
    }

    private static async Task SeedApprovalCaseAsync(
        GarageContext context,
        decimal unitPrice,
        decimal discountValue,
        string quotationStatus)
    {
        var branch = new Branch
        {
            Id = 1,
            Name = "Chi nhánh 1",
            ManagerId = 300
        };
        var serviceOrder = new ServiceOrder
        {
            Id = 1,
            Code = "SO-001",
            CustomerId = 10,
            VehicleId = 20,
            OriginBranchId = 1,
            ExecuteBranchId = 1,
            Status = "IN_PROGRESS"
        };
        var quotation = new Quotation
        {
            Id = 1,
            Code = "Q-001",
            ServiceOrderId = 1,
            QuotationType = "INSURANCE",
            VersionNo = 1,
            InsuranceChangeType = "NONE",
            Status = quotationStatus,
            CustomerDecisionStatus = "PENDING",
            TechnicalApprovalStatus = "PENDING",
            FinancialApprovalStatus = "NOT_REQUIRED",
            RequiresFinancialApproval = false
        };
        var detail = new QuotationDetail
        {
            Id = 1,
            QuotationId = 1,
            ServiceOrderId = 1,
            ItemType = "PART",
            ItemName = "Lọc dầu",
            Quantity = 1,
            UnitPriceAtTime = unitPrice,
            DiscountValue = discountValue,
            TaxRate = 0,
            SubTotal = unitPrice,
            TaxAmount = 0,
            FinalAmount = unitPrice - discountValue,
            PayerType = "CUSTOMER",
            ApprovalStatus = "PENDING",
            Status = "ACTIVE",
            IsBlocking = true,
            IsMandatory = true
        };
        var policy = new BranchQuotationApprovalPolicy
        {
            Id = 1,
            BranchId = 1,
            MaxDiscountPercentWithoutManager = 5m,
            HighValueThreshold = 50_000_000m,
            IsActive = true
        };

        context.Branches.Add(branch);
        context.ServiceOrders.Add(serviceOrder);
        context.Quotations.Add(quotation);
        context.QuotationDetails.Add(detail);
        context.BranchQuotationApprovalPolicies.Add(policy);
        await context.SaveChangesAsync();
    }

    private static async Task SeedPendingFinancialApprovalCaseAsync(GarageContext context)
    {
        await SeedApprovalCaseAsync(
            context,
            unitPrice: 2_000_000m,
            discountValue: 200_000m,
            quotationStatus: "PENDING_BRANCH_MANAGER");

        var quotation = await context.Quotations.SingleAsync(x => x.Id == 1);
        quotation.TechnicalApprovalStatus = "APPROVED";
        quotation.TechnicalApprovedBy = 200;
        quotation.TechnicalApprovedAt = DateTime.UtcNow;
        quotation.RequiresFinancialApproval = true;
        quotation.FinancialApprovalStatus = "PENDING";

        var detail = await context.QuotationDetails.SingleAsync(x => x.Id == 1);
        detail.ApprovalStatus = "APPROVED";

        await context.SaveChangesAsync();
    }
}
