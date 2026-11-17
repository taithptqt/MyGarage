using Microsoft.Extensions.DependencyInjection;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class SolidResponsibilitySplitTests
{
    [Fact]
    public async Task DiagnosisKpi_UsesLatestSnapshotsAndRetainsBranchScope()
    {
        await using var factory = new TestWebApplicationFactory();
        var date = new DateTime(2026, 1, 10, 10, 0, 0);
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceOrders.AddRange(
                new ServiceOrder { Id = 801, Code = "KPI-1", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "DIAGNOSING" },
                new ServiceOrder { Id = 802, Code = "KPI-2", CustomerId = 100, VehicleId = 1, OriginBranchId = 2, ExecuteBranchId = 2, Status = "DIAGNOSING" });
            db.ServiceOrderSnapshots.AddRange(
                new ServiceOrderSnapshot { Id = 901, ServiceOrderId = 801, RevisionNumber = 1, CreatedAt = date, SnapshotData = "{\"stage\":\"CHECK_IN_T1\",\"intakePackageCode\":\"TEST\"}" },
                new ServiceOrderSnapshot { Id = 902, ServiceOrderId = 801, RevisionNumber = 2, CreatedAt = date, SnapshotData = "{\"stage\":\"AFTER_DIAGNOSIS_V1\",\"customerApproved\":true}" },
                new ServiceOrderSnapshot { Id = 903, ServiceOrderId = 802, RevisionNumber = 1, CreatedAt = date, SnapshotData = "{\"stage\":\"CHECK_IN_T1\",\"intakePackageCode\":\"TEST\"}" });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var direct = scope.ServiceProvider.GetRequiredService<IDiagnosisIntakeKpiService>();
        var facade = scope.ServiceProvider.GetRequiredService<IDiagnosisHandoverSnapshotService>();
        var result = await direct.GetIntakeConversionKpiAsync(1, date.Date, date.Date);
        var forwarded = await facade.GetIntakeConversionKpiAsync(1, date.Date, date.Date);
        Assert.Equal(1, result.TotalIntakeOrders);
        Assert.Equal(1, result.TotalApprovedAdditionalRepairOrders);
        Assert.Equal(result.TotalIntakeOrders, forwarded.TotalIntakeOrders);
        Assert.Equal(result.TotalApprovedAdditionalRepairOrders, forwarded.TotalApprovedAdditionalRepairOrders);
    }

    [Fact]
    public async Task InventoryWorkflow_DelegatesToSeparatedServices()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var scope = factory.Services.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IInventoryAdminWorkflowService>();
        var alerts = scope.ServiceProvider.GetRequiredService<IInventoryAdminStockAlertService>();
        var approvals = scope.ServiceProvider.GetRequiredService<IInventoryAdminApprovalService>();
        Assert.Empty(await alerts.GetStockAlertsAsync());
        Assert.Empty(await workflow.GetStockAlertsAsync());
        Assert.Empty(await approvals.GetApprovalsAsync(null));
        Assert.Empty(await workflow.GetApprovalsAsync(null));
        Assert.False(await workflow.ResolveStockAlertAsync(0, 0));
        Assert.False(await workflow.ApproveRequestAsync(-1, 200, null));
    }
}