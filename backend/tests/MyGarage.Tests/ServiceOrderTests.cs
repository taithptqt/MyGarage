using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Domain.Enums;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class ServiceOrderTests
{
    // ==========================================
    // 1. Pure Domain Lifecycle & Invariant Tests
    // ==========================================

    [Theory]
    [InlineData("NEW", "CHECKED_IN", true)]
    [InlineData("NEW", "CANCELED", true)]
    [InlineData("NEW", "CLOSED", false)]
    [InlineData("CHECKED_IN", "DIAGNOSING", true)]
    [InlineData("CHECKED_IN", "APPROVED", true)]
    [InlineData("DIAGNOSING", "QUOTING", true)]
    [InlineData("APPROVED", "IN_PROGRESS", true)]
    [InlineData("IN_PROGRESS", "READY", true)]
    [InlineData("READY", "CLOSED", true)]
    [InlineData("READY", "REPAIRING", true)] // QC Fail rejection
    [InlineData("CLOSED", "READY", false)] // Terminal status cannot be transitioned from
    [InlineData("CANCELED", "IN_PROGRESS", false)] // Terminal status cannot be transitioned from
    [InlineData("IN_PROGRESS", "IN_PROGRESS", true)] // No-op transition
    public void ServiceOrderLifecycleRules_CanTransition_ValidatesCorrectly(string currentStatus, string targetStatus, bool expected)
    {
        var result = ServiceOrderLifecycleRules.CanTransition(currentStatus, targetStatus);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("NEW", true)]
    [InlineData("CHECKED_IN", true)]
    [InlineData("IN_PROGRESS", true)]
    [InlineData("READY", false)]
    [InlineData("WAITING_PAYMENT", false)]
    [InlineData("CLOSED", false)]
    [InlineData("CANCELED", false)]
    public void ServiceOrderLifecycleRules_CanEditWorkshop_ValidatesLockStatus(string status, bool expected)
    {
        var result = ServiceOrderLifecycleRules.CanEditWorkshop(status);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ServiceOrderLifecycleRules_EvaluateHandoverReadiness_BlocksUnpaidOrder()
    {
        var input = new ServiceOrderLifecycleRules.HandoverEvaluationInput(
            OrderStatus: "READY",
            PaymentStatus: "UNPAID",
            QcStatus: "PASS",
            WorkDetailStatuses: new List<string> { "COMPLETED" }
        );

        var result = ServiceOrderLifecycleRules.EvaluateHandoverReadiness(input);
        Assert.False(result.CanHandover);
        Assert.Contains("PaymentStatus != PAID", result.BlockingReason);
    }

    [Fact]
    public void ServiceOrderLifecycleRules_EvaluateHandoverReadiness_BlocksQcFailedOrder()
    {
        var input = new ServiceOrderLifecycleRules.HandoverEvaluationInput(
            OrderStatus: "READY",
            PaymentStatus: "PAID",
            QcStatus: "FAIL",
            WorkDetailStatuses: new List<string> { "COMPLETED" }
        );

        var result = ServiceOrderLifecycleRules.EvaluateHandoverReadiness(input);
        Assert.False(result.CanHandover);
        Assert.Contains("QcStatus != PASS", result.BlockingReason);
    }

    [Fact]
    public void ServiceOrderLifecycleRules_EvaluateHandoverReadiness_BlocksIncompleteWorkTasks()
    {
        var input = new ServiceOrderLifecycleRules.HandoverEvaluationInput(
            OrderStatus: "READY",
            PaymentStatus: "PAID",
            QcStatus: "PASS",
            WorkDetailStatuses: new List<string> { "COMPLETED", "IN_PROGRESS" }
        );

        var result = ServiceOrderLifecycleRules.EvaluateHandoverReadiness(input);
        Assert.False(result.CanHandover);
        Assert.Contains("chưa hoàn thành", result.BlockingReason);
    }

    [Fact]
    public void ServiceOrderLifecycleRules_EvaluateHandoverReadiness_AllowsWhenAllConditionsMet()
    {
        var input = new ServiceOrderLifecycleRules.HandoverEvaluationInput(
            OrderStatus: "READY",
            PaymentStatus: "PAID",
            QcStatus: "PASS",
            WorkDetailStatuses: new List<string> { "COMPLETED", "COMPLETED" }
        );

        var result = ServiceOrderLifecycleRules.EvaluateHandoverReadiness(input);
        Assert.True(result.CanHandover);
        Assert.Null(result.BlockingReason);
    }

    [Fact]
    public void ServiceOrderLifecycleRules_ValidateBayAllocation_ValidatesBranchAndTonnage()
    {
        // Cross-branch allocation fails
        var crossBranch = new ServiceOrderLifecycleRules.BayAllocationInput(
            BayBranchId: 1,
            OrderExecuteBranchId: 2,
            BayOperationalStatus: "ACTIVE",
            BayMaxTonnage: 3.5m,
            VehicleTonnage: 2.0m,
            IsBayCurrentlyOccupied: false
        );
        var r1 = ServiceOrderLifecycleRules.ValidateBayAllocation(crossBranch);
        Assert.False(r1.IsValid);
        Assert.Contains("chi nhánh", r1.ErrorMessage);

        // Over-tonnage fails
        var overTonnage = new ServiceOrderLifecycleRules.BayAllocationInput(
            BayBranchId: 1,
            OrderExecuteBranchId: 1,
            BayOperationalStatus: "ACTIVE",
            BayMaxTonnage: 2.0m,
            VehicleTonnage: 3.5m,
            IsBayCurrentlyOccupied: false
        );
        var r2 = ServiceOrderLifecycleRules.ValidateBayAllocation(overTonnage);
        Assert.False(r2.IsValid);
        Assert.Contains("vượt quá", r2.ErrorMessage);

        // Occupied bay fails
        var occupied = new ServiceOrderLifecycleRules.BayAllocationInput(
            BayBranchId: 1,
            OrderExecuteBranchId: 1,
            BayOperationalStatus: "ACTIVE",
            BayMaxTonnage: 3.5m,
            VehicleTonnage: 2.0m,
            IsBayCurrentlyOccupied: true
        );
        var r3 = ServiceOrderLifecycleRules.ValidateBayAllocation(occupied);
        Assert.False(r3.IsValid);
        Assert.Contains("phương tiện khác chiếm chỗ", r3.ErrorMessage);

        // Valid allocation passes
        var valid = new ServiceOrderLifecycleRules.BayAllocationInput(
            BayBranchId: 1,
            OrderExecuteBranchId: 1,
            BayOperationalStatus: "ACTIVE",
            BayMaxTonnage: 3.5m,
            VehicleTonnage: 2.0m,
            IsBayCurrentlyOccupied: false
        );
        var r4 = ServiceOrderLifecycleRules.ValidateBayAllocation(valid);
        Assert.True(r4.IsValid);
        Assert.Null(r4.ErrorMessage);
    }

    [Fact]
    public void ServiceOrderLifecycleRules_IsTechnicianQualified_ChecksSpecialtyCorrectly()
    {
        Assert.True(ServiceOrderLifecycleRules.IsTechnicianQualified("ENGINE_EXPERT", "GENERAL"));
        Assert.True(ServiceOrderLifecycleRules.IsTechnicianQualified("MASTER", "TRANSMISSION"));
        Assert.True(ServiceOrderLifecycleRules.IsTechnicianQualified("ELECTRICAL,ENGINE", "ENGINE"));
        Assert.False(ServiceOrderLifecycleRules.IsTechnicianQualified("BODY_PAINT", "ENGINE"));
        Assert.False(ServiceOrderLifecycleRules.IsTechnicianQualified(null, "ENGINE"));
    }

    [Fact]
    public void ServiceOrderLifecycleRules_ValidateSplitRatios_RequiresSumToOne()
    {
        Assert.True(ServiceOrderLifecycleRules.ValidateSplitRatios(new[] { 0.6m, 0.4m }));
        Assert.True(ServiceOrderLifecycleRules.ValidateSplitRatios(new[] { 1.0m }));
        Assert.False(ServiceOrderLifecycleRules.ValidateSplitRatios(new[] { 0.5m, 0.3m })); // Sum = 0.8
        Assert.False(ServiceOrderLifecycleRules.ValidateSplitRatios(new[] { -0.2m, 1.2m })); // Negative ratio
        Assert.False(ServiceOrderLifecycleRules.ValidateSplitRatios(null));
    }

    // ==========================================
    // 2. Integration: Cross-Branch Isolation Tests
    // ==========================================

    [Fact]
    public async Task ServiceOrders_GetAll_EnforcesExecuteBranchIdIsolation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.ServiceOrders.Add(new ServiceOrder
            {
                Id = 9101,
                Code = "SO-B1-001",
                CustomerId = 1,
                VehicleId = 1,
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                Status = "IN_PROGRESS",
                CreatedAt = DateTime.UtcNow
            });

            db.ServiceOrders.Add(new ServiceOrder
            {
                Id = 9102,
                Code = "SO-B2-001",
                CustomerId = 2,
                VehicleId = 2,
                OriginBranchId = 2,
                ExecuteBranchId = 2,
                Status = "IN_PROGRESS",
                CreatedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();

        // 1. Staff at Branch 1 gets only Branch 1 orders
        var staffBranch1Token = TestWebApplicationFactory.CreateJwtToken(userId: 200, role: "Staff", roleCode: "STAFF", branchId: 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffBranch1Token);

        var response = await client.GetAsync("/api/v1/service-orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var list = System.Text.Json.JsonSerializer.Deserialize<List<ServiceOrderResponseDto>>(
            doc.RootElement.GetProperty("items").GetRawText(),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(list);
        Assert.Contains(list, o => o.Id == 9101);
        Assert.DoesNotContain(list, o => o.Id == 9102);

        // 2. Staff at Branch 1 accessing Branch 2 order directly via GetById receives 403 Forbidden
        var singleForbidden = await client.GetAsync("/api/v1/service-orders/9102");
        Assert.Equal(HttpStatusCode.Forbidden, singleForbidden.StatusCode);

        // 3. Admin can access Branch 2 order
        var adminToken = TestWebApplicationFactory.CreateJwtToken(userId: 201, role: "Staff", roleCode: "ADMIN", branchId: 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var adminResponse = await client.GetAsync("/api/v1/service-orders/9102");
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task ServiceOrders_DualRouting_SupportsV1Prefix()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceOrders.Add(new ServiceOrder
            {
                Id = 9105,
                Code = "SO-B1-005",
                CustomerId = 1,
                VehicleId = 1,
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                Status = "IN_PROGRESS",
                CreatedAt = DateTime.UtcNow
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var adminToken = TestWebApplicationFactory.CreateJwtToken(userId: 201, role: "Staff", roleCode: "ADMIN", branchId: 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // GET /api/v1/service-orders/9105
        var v1Response = await client.GetAsync("/api/v1/service-orders/9105");
        Assert.Equal(HttpStatusCode.OK, v1Response.StatusCode);

        var dto = await v1Response.Content.ReadFromJsonAsync<ServiceOrderResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(9105, dto.Id);
    }

    // ==========================================
    // 3. Handover Invariants & ExecutionStrategy
    // ==========================================

    [Fact]
    public async Task ServiceOrders_ProcessHandover_EnforcesQcAndPaymentPrerequisites()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.Staffs.Add(new Staff
            {
                Id = 205,
                FullName = "Advisor 205",
                BranchId = 1,
                Status = "ACTIVE",
                PasswordHash = "test-hash",
                SecurityStamp = "test-stamp"
            });

            // Unpaid, non-QC order
            db.ServiceOrders.Add(new ServiceOrder
            {
                Id = 9201,
                Code = "SO-UNPAID-001",
                CustomerId = 1,
                VehicleId = 1,
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                Status = "READY",
                PaymentStatus = "UNPAID",
                Qcstatus = "PENDING",
                CreatedAt = DateTime.UtcNow
            });

            // Ready, fully paid and QC passed order
            db.ServiceOrders.Add(new ServiceOrder
            {
                Id = 9202,
                Code = "SO-READY-002",
                CustomerId = 1,
                VehicleId = 1,
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                Status = "READY",
                PaymentStatus = "PAID",
                Qcstatus = "PASS",
                CreatedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var advisorToken = TestWebApplicationFactory.CreateJwtToken(userId: 205, role: "Staff", roleCode: "SERVICE_ADVISOR", branchId: 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", advisorToken);

        // 1. Attempting handover on unpaid order 9201 returns 400 Bad Request
        var failPayload = new HandoverRequestDto
        {
            StaffId = 205,
            ReceiverName = "Nguyen Van A"
        };
        var failResponse = await client.PostAsJsonAsync("/api/v1/service-orders/9201/handovers", failPayload);
        Assert.Equal(HttpStatusCode.BadRequest, failResponse.StatusCode);

        // 2. Handover on 9202 succeeds and closes order
        var passPayload = new HandoverRequestDto
        {
            HandedOverBy = 205,
            ReceiverName = "Nguyen Van A",
            ReceiverPhone = "0901234567"
        };
        var passResponse = await client.PostAsJsonAsync("/api/v1/service-orders/9202/handovers", passPayload);
        Assert.Equal(HttpStatusCode.OK, passResponse.StatusCode);

        // Verify order status changed to CLOSED in database
        using var scope = factory.Services.CreateScope();
        var verifyDb = scope.ServiceProvider.GetRequiredService<MyGarage.Data.GarageContext>();
        var closedOrder = await verifyDb.ServiceOrders.FirstOrDefaultAsync(o => o.Id == 9202);
        Assert.NotNull(closedOrder);
        Assert.Equal("CLOSED", closedOrder.Status);
        Assert.Equal(205, closedOrder.UpdatedBy);
    }

    // ==========================================
    // 4. Separation of Duties: QC Inspection
    // ==========================================

    [Fact]
    public async Task QcInspection_EnforcesSeparationOfDuties_TechnicianCannotInspectOwnRepairs()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.Staffs.Add(new Staff
            {
                Id = 21,
                FullName = "Tech 21",
                BranchId = 1,
                Status = "ACTIVE",
                PasswordHash = "test-hash",
                SecurityStamp = "test-stamp"
            });

            db.Staffs.Add(new Staff
            {
                Id = 22,
                FullName = "Inspector 22",
                BranchId = 1,
                Status = "ACTIVE",
                PasswordHash = "test-hash",
                SecurityStamp = "test-stamp"
            });

            db.ServiceOrders.Add(new ServiceOrder
            {
                Id = 9301,
                Code = "SO-QC-001",
                CustomerId = 1,
                VehicleId = 1,
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                Status = "IN_PROGRESS",
                CreatedAt = DateTime.UtcNow
            });

            db.ServiceOrderDetails.Add(new ServiceOrderDetail
            {
                Id = 9311,
                ServiceOrderId = 9301,
                ItemType = "LABOR",
                ItemName = "Bảo dưỡng định kỳ",
                PayerType = "CUSTOMER",
                ServiceSource = "STANDARD",
                Unit = "LẦN",
                TechnicianId = 21, // Tech 21 performed work
                Status = "COMPLETED"
            });

            db.ServiceStaffAssignments.Add(new ServiceStaffAssignment
            {
                Id = 9321,
                ServiceOrderDetailId = 9311,
                TechnicianId = 21, // Assigned to Tech 21
                RoleInTask = "PRIMARY",
                SplitRatio = 1.0m
            });

            db.QcInspections.Add(new QcInspection
            {
                Id = 9351,
                ServiceOrderId = 9301,
                QcStaffId = 22,
                Status = "PENDING",
                InspectedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();

        // 1. Tech 21 (who worked on the order) attempts to Pass inspection -> 400 Bad Request (Separation of duties)
        var tech21Token = TestWebApplicationFactory.CreateJwtToken(userId: 21, role: "Staff", roleCode: "QC_INSPECTOR", branchId: 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tech21Token);

        var selfInspectResponse = await client.PostAsJsonAsync("/api/v1/qc-inspections/9351/passes", new QcPassRequestDto
        {
            InspectorId = 21,
            Notes = "Self-inspection attempt"
        });
        Assert.Equal(HttpStatusCode.BadRequest, selfInspectResponse.StatusCode);

        // 2. Inspector 22 (who DID NOT work on the order) passes inspection -> 200 OK
        var inspector22Token = TestWebApplicationFactory.CreateJwtToken(userId: 22, role: "Staff", roleCode: "QC_INSPECTOR", branchId: 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", inspector22Token);

        var validInspectResponse = await client.PostAsJsonAsync("/api/v1/qc-inspections/9351/passes", new QcPassRequestDto
        {
            InspectorId = 22,
            Notes = "Inspection verified by independent QC"
        });
        Assert.Equal(HttpStatusCode.OK, validInspectResponse.StatusCode);
    }
}
