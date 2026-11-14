using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Application.Authorization;
using MyGarage.Application.GatePasses;
using MyGarage.Common;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class SharedOperationsAndAuditModuleTests
{
    // =========================================================================
    // SECTION 1: SharedOperationsDomainRules Unit Tests
    // =========================================================================

    [Theory]
    [InlineData("pending", "PENDING")]
    [InlineData(" approved ", "APPROVED")]
    [InlineData("in-progress", "IN_PROGRESS")]
    [InlineData("gate pass", "GATE_PASS")]
    [InlineData("cancelled", "CANCELED")]
    [InlineData("CANCELLED", "CANCELED")]
    [InlineData("canceled", "CANCELED")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void GatePassRules_NormalizesStatusCorrectly(string? raw, string expected)
    {
        Assert.Equal(expected, SharedOperationsDomainRules.NormalizeStatus(raw));
    }

    [Fact]
    public void GatePassRules_EvaluatesFinalizationAndMutationValidation()
    {
        Assert.False(SharedOperationsDomainRules.IsGatePassFinalized("PENDING", null));
        Assert.False(SharedOperationsDomainRules.IsGatePassFinalized("APPROVED", null));
        Assert.True(SharedOperationsDomainRules.IsGatePassFinalized("SCANNED", null));
        Assert.True(SharedOperationsDomainRules.IsGatePassFinalized("PENDING", VietnamTime.Now));

        var (okOpen, errOpen) = SharedOperationsDomainRules.ValidateGatePassMutation("PENDING", null, isServiceOrderClosedLocked: false);
        Assert.True(okOpen);
        Assert.Null(errOpen);

        var (okScanned, errScanned) = SharedOperationsDomainRules.ValidateGatePassMutation("SCANNED", null, isServiceOrderClosedLocked: false);
        Assert.False(okScanned);
        Assert.NotNull(errScanned);

        var (okPassedAt, errPassedAt) = SharedOperationsDomainRules.ValidateGatePassMutation("APPROVED", VietnamTime.Now, isServiceOrderClosedLocked: false);
        Assert.False(okPassedAt);
        Assert.NotNull(errPassedAt);

        var (okClosedSo, errClosedSo) = SharedOperationsDomainRules.ValidateGatePassMutation("PENDING", null, isServiceOrderClosedLocked: true);
        Assert.False(okClosedSo);
        Assert.NotNull(errClosedSo);
    }

    [Fact]
    public void GatePassRules_EnforcesStatusTransitions()
    {
        // Forward from PENDING
        Assert.True(SharedOperationsDomainRules.CanTransitionGatePassStatus("PENDING", "APPROVED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionGatePassStatus("PENDING", "SCANNED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionGatePassStatus("PENDING", "CANCELED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionGatePassStatus(null, "APPROVED"));

        // Forward from APPROVED
        Assert.True(SharedOperationsDomainRules.CanTransitionGatePassStatus("APPROVED", "SCANNED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionGatePassStatus("APPROVED", "CANCELLED"));
        Assert.False(SharedOperationsDomainRules.CanTransitionGatePassStatus("APPROVED", "PENDING"));

        // Idempotent same-status
        Assert.True(SharedOperationsDomainRules.CanTransitionGatePassStatus("PENDING", "PENDING"));
        Assert.True(SharedOperationsDomainRules.CanTransitionGatePassStatus("APPROVED", "APPROVED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionGatePassStatus("SCANNED", "SCANNED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionGatePassStatus("CANCELED", "CANCELLED"));

        // Terminal states cannot transition to other states
        Assert.False(SharedOperationsDomainRules.CanTransitionGatePassStatus("SCANNED", "PENDING"));
        Assert.False(SharedOperationsDomainRules.CanTransitionGatePassStatus("SCANNED", "APPROVED"));
        Assert.False(SharedOperationsDomainRules.CanTransitionGatePassStatus("SCANNED", "CANCELED"));
        Assert.False(SharedOperationsDomainRules.CanTransitionGatePassStatus("CANCELED", "PENDING"));
        Assert.False(SharedOperationsDomainRules.CanTransitionGatePassStatus("CANCELED", "SCANNED"));
        Assert.False(SharedOperationsDomainRules.CanTransitionGatePassStatus("PENDING", "UNKNOWN"));
    }

    [Fact]
    public void ApprovalRequestRules_EnforcesTransitionsAndInputValidation()
    {
        Assert.True(SharedOperationsDomainRules.CanTransitionApprovalStatus("PENDING", "APPROVED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionApprovalStatus("PENDING", "REJECTED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionApprovalStatus("PENDING", "CANCELED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionApprovalStatus("PENDING", "CANCELLED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionApprovalStatus(null, "APPROVED"));

        // Idempotent same-status
        Assert.True(SharedOperationsDomainRules.CanTransitionApprovalStatus("APPROVED", "APPROVED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionApprovalStatus("REJECTED", "REJECTED"));
        Assert.True(SharedOperationsDomainRules.CanTransitionApprovalStatus("CANCELED", "CANCELLED"));

        // Block reverse transitions
        Assert.False(SharedOperationsDomainRules.CanTransitionApprovalStatus("APPROVED", "REJECTED"));
        Assert.False(SharedOperationsDomainRules.CanTransitionApprovalStatus("REJECTED", "APPROVED"));
        Assert.False(SharedOperationsDomainRules.CanTransitionApprovalStatus("CANCELED", "APPROVED"));

        // Input validation
        var (okValid, errValid) = SharedOperationsDomainRules.ValidateApprovalRequestInput("GATE_PASS", 15, "Xe ra cổng bảo hành");
        Assert.True(okValid);
        Assert.Null(errValid);

        var (okEmptyType, _) = SharedOperationsDomainRules.ValidateApprovalRequestInput(" ", 15, "Lý do");
        Assert.False(okEmptyType);

        var (okZeroTarget, _) = SharedOperationsDomainRules.ValidateApprovalRequestInput("DISCOUNT", 0, "Lý do");
        Assert.False(okZeroTarget);

        var (okNullTarget, _) = SharedOperationsDomainRules.ValidateApprovalRequestInput("DISCOUNT", null, "Lý do");
        Assert.False(okNullTarget);
    }

    [Fact]
    public void AuditAndSecurityRules_NormalizesPagingAndDetectsSensitiveFields()
    {
        Assert.Equal((1, 50), SharedOperationsDomainRules.NormalizeAuditPageSize(0, 0));
        Assert.Equal((1, 50), SharedOperationsDomainRules.NormalizeAuditPageSize(-3, -10));
        Assert.Equal((2, 25), SharedOperationsDomainRules.NormalizeAuditPageSize(2, 25));
        Assert.Equal((3, 200), SharedOperationsDomainRules.NormalizeAuditPageSize(3, 500));

        Assert.True(SharedOperationsDomainRules.IsSensitiveAuditField("password"));
        Assert.True(SharedOperationsDomainRules.IsSensitiveAuditField("PasswordHash"));
        Assert.True(SharedOperationsDomainRules.IsSensitiveAuditField("salt"));
        Assert.True(SharedOperationsDomainRules.IsSensitiveAuditField("apiKey"));
        Assert.True(SharedOperationsDomainRules.IsSensitiveAuditField("api_key"));
        Assert.True(SharedOperationsDomainRules.IsSensitiveAuditField("securityStamp"));
        Assert.True(SharedOperationsDomainRules.IsSensitiveAuditField("security_stamp"));
        Assert.True(SharedOperationsDomainRules.IsSensitiveAuditField("resetToken"));
        Assert.True(SharedOperationsDomainRules.IsSensitiveAuditField("client_secret"));
        Assert.True(SharedOperationsDomainRules.IsSensitiveAuditField("otp_code"));

        Assert.False(SharedOperationsDomainRules.IsSensitiveAuditField("FullName"));
        Assert.False(SharedOperationsDomainRules.IsSensitiveAuditField("BranchId"));
        Assert.False(SharedOperationsDomainRules.IsSensitiveAuditField("Status"));
        Assert.False(SharedOperationsDomainRules.IsSensitiveAuditField(null));
        Assert.False(SharedOperationsDomainRules.IsSensitiveAuditField("   "));
    }

    // =========================================================================
    // SECTION 2: SharedOperationsAuthorizationService Tests
    // =========================================================================

    [Fact]
    public async Task SharedOperationsAuthorizationService_EnforcesBranchAndRoleIsolation()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"SharedOpsAuthDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Staffs.AddRange(
            new Staff { Id = 200, FullName = "Quản lý CN1", Phone = "0901000200", Email = "m1@example.com", PasswordHash = "hash", BranchId = 1, Status = "ACTIVE" },
            new Staff { Id = 202, FullName = "Quản lý CN2", Phone = "0901000202", Email = "m2@example.com", PasswordHash = "hash", BranchId = 2, Status = "ACTIVE" },
            new Staff { Id = 205, FullName = "Kỹ thuật viên CN1", Phone = "0901000205", Email = "t1@example.com", PasswordHash = "hash", BranchId = 1, Status = "ACTIVE" }
        );
        db.ServiceOrders.Add(new ServiceOrder
        {
            Id = 500,
            Code = "SO-500",
            CustomerId = 100,
            VehicleId = 10,
            OriginBranchId = 1,
            ExecuteBranchId = 1,
            Status = "IN_PROGRESS"
        });
        db.GatePasses.Add(new GatePass
        {
            Id = 600,
            ServiceOrderId = 500,
            ClearanceType = "NORMAL",
            QrCode = "GP-600",
            IssuedBy = 205,
            Status = SharedOperationsDomainRules.StatusPending
        });
        db.ApprovalRequests.Add(new ApprovalRequest
        {
            Id = 700,
            RequestType = "GATE_PASS",
            GatePassId = 600,
            RequestedBy = 205,
            Status = SharedOperationsDomainRules.ApprovalPending
        });
        await db.SaveChangesAsync();

        var auth = new SharedOperationsAuthorizationService(db);

        var adminActor = new UserActorContext(201, "Staff", "ADMIN", BranchId: 1, IsAdmin: true);
        var managerBranch1 = new UserActorContext(200, "Staff", "MANAGER", BranchId: 1, IsAdmin: false);
        var managerBranch2 = new UserActorContext(202, "Staff", "MANAGER", BranchId: 2, IsAdmin: false);
        var techBranch1 = new UserActorContext(205, "Staff", "TECHNICIAN", BranchId: 1, IsAdmin: false);
        var customerActor = new UserActorContext(100, "Customer", "CUSTOMER", BranchId: null, IsAdmin: false);

        // Audit logs: Admin only
        Assert.True(await auth.CanAccessAuditLogsAsync(adminActor));
        Assert.False(await auth.CanAccessAuditLogsAsync(managerBranch1));
        Assert.False(await auth.CanAccessAuditLogsAsync(customerActor));

        // GatePass management: Admin or matching branch Staff
        Assert.True(await auth.CanManageGatePassesAsync(1, adminActor));
        Assert.True(await auth.CanManageGatePassesAsync(2, adminActor));
        Assert.True(await auth.CanManageGatePassesAsync(1, managerBranch1));
        Assert.False(await auth.CanManageGatePassesAsync(1, managerBranch2));
        Assert.False(await auth.CanManageGatePassesAsync(1, customerActor));

        // Approval request access & decision
        Assert.True(await auth.CanAccessApprovalRequestsAsync(1, managerBranch1));
        Assert.False(await auth.CanAccessApprovalRequestsAsync(1, managerBranch2));
        Assert.False(await auth.CanAccessApprovalRequestsAsync(1, customerActor));

        Assert.True(await auth.CanDecideApprovalRequestAsync(700, adminActor));
        Assert.True(await auth.CanDecideApprovalRequestAsync(700, managerBranch1));
        Assert.False(await auth.CanDecideApprovalRequestAsync(700, managerBranch2)); // Wrong branch
        Assert.False(await auth.CanDecideApprovalRequestAsync(700, techBranch1)); // Technician cannot decide
        Assert.False(await auth.CanDecideApprovalRequestAsync(700, customerActor));
    }

    // =========================================================================
    // SECTION 3: GatePassAndApprovalApplicationService Integration Tests
    // =========================================================================

    [Fact]
    public async Task GatePassAndApprovalApplicationService_ExecutesGatePassAndApprovalLifecycle()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"SharedOpsAppDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Branches.AddRange(
            new Branch { Id = 1, Name = "Chi nhánh 1", Status = "ACTIVE" },
            new Branch { Id = 2, Name = "Chi nhánh 2", Status = "ACTIVE" }
        );
        db.Staffs.AddRange(
            new Staff { Id = 200, FullName = "Quản lý CN1", Phone = "0901000200", Email = "m1@example.com", PasswordHash = "hash", BranchId = 1, Status = "ACTIVE" },
            new Staff { Id = 202, FullName = "Quản lý CN2", Phone = "0901000202", Email = "m2@example.com", PasswordHash = "hash", BranchId = 2, Status = "ACTIVE" },
            new Staff { Id = 205, FullName = "Cố vấn dịch vụ CN1", Phone = "0901000205", Email = "sa1@example.com", PasswordHash = "hash", BranchId = 1, Status = "ACTIVE" }
        );
        db.ServiceOrders.AddRange(
            new ServiceOrder
            {
                Id = 501,
                Code = "SO-501",
                CustomerId = 100,
                VehicleId = 10,
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                Status = "READY_FOR_HANDOVER"
            },
            new ServiceOrder
            {
                Id = 502,
                Code = "SO-502",
                CustomerId = 100,
                VehicleId = 10,
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                Status = "CLOSED"
            }
        );
        await db.SaveChangesAsync();

        var auth = new SharedOperationsAuthorizationService(db);
        var service = new GatePassAndApprovalApplicationService(db, auth);

        var advisorBranch1 = new UserActorContext(205, "Staff", "SERVICE_ADVISOR", BranchId: 1, IsAdmin: false);
        var managerBranch1 = new UserActorContext(200, "Staff", "MANAGER", BranchId: 1, IsAdmin: false);
        var managerBranch2 = new UserActorContext(202, "Staff", "MANAGER", BranchId: 2, IsAdmin: false);

        // 1. Issue GatePass on open ServiceOrder
        var issued = await service.IssueGatePassAsync(
            new IssueGatePassCommand(
                ServiceOrderId: 501,
                ClearanceType: "NORMAL",
                QrCode: "GP-SO501-001",
                ResolutionNotes: "Bàn giao xe sau bảo dưỡng"),
            advisorBranch1);

        Assert.True(issued.Id > 0);
        Assert.Equal(SharedOperationsDomainRules.StatusPending, issued.Status);
        Assert.Equal(205, issued.IssuedBy);

        // Cannot issue GatePass on CLOSED ServiceOrder
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IssueGatePassAsync(
                new IssueGatePassCommand(ServiceOrderId: 502),
                advisorBranch1));

        // Cross-branch staff cannot issue GatePass for Branch 1 ServiceOrder
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.IssueGatePassAsync(
                new IssueGatePassCommand(ServiceOrderId: 501),
                managerBranch2));

        // 2. Create ApprovalRequest for the GatePass
        var approvalReq = await service.CreateApprovalRequestAsync(
            new CreateApprovalRequestCommand(
                RequestType: "GATE_PASS",
                GatePassId: issued.Id,
                Reason: "Xin phê duyệt phiếu xuất cổng",
                BranchId: 1),
            advisorBranch1);

        Assert.True(approvalReq.Id > 0);
        Assert.Equal(SharedOperationsDomainRules.ApprovalPending, approvalReq.Status);

        // 3. Manager Branch 1 approves the request -> GatePass transitions to APPROVED
        var decided = await service.DecideApprovalRequestAsync(
            approvalReq.Id,
            new DecideApprovalRequestCommand(TargetStatus: SharedOperationsDomainRules.ApprovalApproved),
            managerBranch1);

        Assert.NotNull(decided);
        Assert.Equal(SharedOperationsDomainRules.ApprovalApproved, decided!.Status);
        Assert.Equal(200, decided.ApproverId);

        var gatePassInDb = await db.GatePasses.AsNoTracking().FirstAsync(g => g.Id == issued.Id);
        Assert.Equal(SharedOperationsDomainRules.StatusApproved, gatePassInDb.Status);
        Assert.Equal(200, gatePassInDb.ApprovedBy);

        // 4. Scan GatePass at security gate -> transitions to SCANNED and sets PassedAt
        var scanned = await service.ScanGatePassAsync(
            issued.Id,
            new ScanGatePassCommand(IsOldPartsChecked: true, ResolutionNotes: "Đã kiểm tra phụ tùng cũ"),
            managerBranch1);

        Assert.NotNull(scanned);
        Assert.Equal(SharedOperationsDomainRules.StatusScanned, scanned!.Status);
        Assert.Equal(200, scanned.ScannedBy);
        Assert.NotNull(scanned.PassedAt);
        Assert.NotNull(scanned.SecurityCheckedAt);
        Assert.True(scanned.IsOldPartsChecked);

        // Cannot scan an already-finalized GatePass again
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ScanGatePassAsync(
                issued.Id,
                new ScanGatePassCommand(IsOldPartsChecked: true),
                managerBranch1));
    }

    // =========================================================================
    // SECTION 4: Web API Dual Routing Tests (9 Controllers)
    // =========================================================================

    [Fact]
    public async Task Module12AndFoundationControllers_EnforceDualRoutes_AcrossAllNineControllers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });

        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGatePassListReader>();
            services.AddScoped<IGatePassListReader, FakeGatePassListReader>();
        }));

        using var adminClient = app.CreateClient();
        var adminToken = TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 1. GatePassController (/api/gate-passes vs /api/v1/gate-passes)
        var gpRes1 = await adminClient.GetAsync("/api/gate-passes");
        var gpRes2 = await adminClient.GetAsync("/api/v1/gate-passes");
        Assert.Equal(HttpStatusCode.OK, gpRes1.StatusCode);
        Assert.Equal(gpRes1.StatusCode, gpRes2.StatusCode);

        // 2. ApprovalRequestController (/api/approval-requests vs /api/v1/approval-requests)
        var arRes1 = await adminClient.GetAsync("/api/approval-requests");
        var arRes2 = await adminClient.GetAsync("/api/v1/approval-requests");
        Assert.Equal(HttpStatusCode.OK, arRes1.StatusCode);
        Assert.Equal(arRes1.StatusCode, arRes2.StatusCode);

        // 3. AuditLogController (/api/audit-logs vs /api/v1/audit-logs)
        var alRes1 = await adminClient.GetAsync("/api/audit-logs");
        var alRes2 = await adminClient.GetAsync("/api/v1/audit-logs");
        Assert.Equal(HttpStatusCode.OK, alRes1.StatusCode);
        Assert.Equal(alRes1.StatusCode, alRes2.StatusCode);

        var aleRes1 = await adminClient.GetAsync("/api/audit-logs/entries");
        var aleRes2 = await adminClient.GetAsync("/api/v1/audit-logs/entries");
        Assert.Equal(HttpStatusCode.OK, aleRes1.StatusCode);
        Assert.Equal(aleRes1.StatusCode, aleRes2.StatusCode);

        // 4. BranchesController (/api/branches vs /api/v1/branches)
        var brRes1 = await adminClient.GetAsync("/api/branches");
        var brRes2 = await adminClient.GetAsync("/api/v1/branches");
        Assert.Equal(HttpStatusCode.OK, brRes1.StatusCode);
        Assert.Equal(brRes1.StatusCode, brRes2.StatusCode);

        // 5. BranchAdministrationController (/api/branches/admin-list vs /api/v1/branches/admin-list)
        var baRes1 = await adminClient.GetAsync("/api/branches/admin-list");
        var baRes2 = await adminClient.GetAsync("/api/v1/branches/admin-list");
        Assert.Equal(HttpStatusCode.OK, baRes1.StatusCode);
        Assert.Equal(baRes1.StatusCode, baRes2.StatusCode);

        // 6. BranchExceptionsController (/api/branch-exceptions vs /api/v1/branch-exceptions)
        var beRes1 = await adminClient.GetAsync("/api/branch-exceptions");
        var beRes2 = await adminClient.GetAsync("/api/v1/branch-exceptions");
        Assert.Equal(HttpStatusCode.OK, beRes1.StatusCode);
        Assert.Equal(beRes1.StatusCode, beRes2.StatusCode);

        // 7. BranchOperatingHoursController (/api/branch-operating-hours vs /api/v1/branch-operating-hours)
        var bohRes1 = await adminClient.GetAsync("/api/branch-operating-hours");
        var bohRes2 = await adminClient.GetAsync("/api/v1/branch-operating-hours");
        Assert.Equal(HttpStatusCode.OK, bohRes1.StatusCode);
        Assert.Equal(bohRes1.StatusCode, bohRes2.StatusCode);

        // 8. RoleController (/api/roles vs /api/v1/roles)
        var roleRes1 = await adminClient.GetAsync("/api/roles");
        var roleRes2 = await adminClient.GetAsync("/api/v1/roles");
        Assert.Equal(HttpStatusCode.OK, roleRes1.StatusCode);
        Assert.Equal(roleRes1.StatusCode, roleRes2.StatusCode);

        // 9. AuthController (/api/auth/session vs /api/v1/auth/session)
        var authRes1 = await adminClient.GetAsync("/api/auth/session");
        var authRes2 = await adminClient.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, authRes1.StatusCode);
        Assert.Equal(authRes1.StatusCode, authRes2.StatusCode);
    }

    [Fact]
    public async Task Module12AndFoundationControllers_AnonymousCallers_RejectedWithUnauthorizedOnProtectedEndpoints()
    {
        await using var factory = new TestWebApplicationFactory();
        using var anonClient = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/gate-passes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/approval-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/branches/admin-list")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.PostAsJsonAsync("/api/v1/branches", new { name = "CN Mới" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.PostAsJsonAsync("/api/v1/branch-exceptions", new { branchId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.PostAsJsonAsync("/api/v1/branch-operating-hours", new { branchId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/v1/auth/profile")).StatusCode);
    }

    private sealed class FakeGatePassListReader : IGatePassListReader
    {
        public Task<(IReadOnlyList<GatePassListItem> Items, int Total)> ReadAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<GatePassListItem> items =
            [
                new GatePassListItem(1, 500, "NORMAL", "GP-500", 200, 201, null, null, false, "APPROVED", null, null, null)
            ];
            return Task.FromResult((items, 1));
        }
    }
}
