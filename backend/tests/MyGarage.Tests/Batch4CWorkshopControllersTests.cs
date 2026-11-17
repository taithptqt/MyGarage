using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Application.Interfaces.Notifications;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Infrastructure.Persistence.Stores.Workshop;
using MyGarage.Infrastructure.Queries;
using MyGarage.Interfaces;
using MyGarage.Mappings;
using MyGarage.Models;
using MyGarage.Repositories;
using MyGarage.Security;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class Batch4CWorkshopControllersTests
{
    private static GarageContext CreateInMemoryContext(string prefix)
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"{prefix}_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new GarageContext(options);
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
            MappingDepthLimit.Apply(cfg);
        }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    // =========================================================================
    // 1. GatePass Domain Rules & GatePassCommandService
    // =========================================================================

    [Theory]
    [InlineData("SCANNED", null, false, false, "Không thể chỉnh sửa phiếu xuất cổng vì xe đã được xác nhận ra cổng.")]
    [InlineData(" scanned ", null, false, false, "Không thể chỉnh sửa phiếu xuất cổng vì xe đã được xác nhận ra cổng.")]
    [InlineData("ISSUED", "2026-11-15T10:00:00Z", false, false, "Không thể chỉnh sửa phiếu xuất cổng vì xe đã được xác nhận ra cổng.")]
    [InlineData("ISSUED", null, true, false, "Không thể chỉnh sửa phiếu xuất cổng vì service order đã đóng.")]
    [InlineData("ISSUED", null, false, true, null)]
    public void SharedOperationsDomainRules_ValidateGatePassMutation_EnforcesFinalizedAndClosedLocks(
        string? status,
        string? passedAtIso,
        bool isClosedLocked,
        bool expectedValid,
        string? expectedMessage)
    {
        DateTime? passedAt = string.IsNullOrWhiteSpace(passedAtIso)
            ? null
            : DateTime.Parse(passedAtIso, null, System.Globalization.DateTimeStyles.RoundtripKind);

        var (isValid, errorMessage) = SharedOperationsDomainRules.ValidateGatePassMutation(
            status,
            passedAt,
            isClosedLocked);

        Assert.Equal(expectedValid, isValid);
        Assert.Equal(expectedMessage, errorMessage);
    }

    [Fact]
    public async Task GatePassCommandService_HandlesCreateUpdateDeleteAndLocks()
    {
        await using var db = CreateInMemoryContext("GatePassCmd");
        var mapper = CreateMapper();
        var gatePassService = new GatePassService(new GatePassRepository(db), mapper);
        var fakeOrderService = new StubServiceOrderService
        {
            CapabilitiesById =
            {
                [10] = new ServiceOrderEditCapabilityResponseDto { ServiceOrderId = 10, IsClosedLocked = false },
                [11] = new ServiceOrderEditCapabilityResponseDto { ServiceOrderId = 11, IsClosedLocked = true }
            }
        };
        var commandService = new GatePassCommandService(gatePassService, fakeOrderService);

        var invalidCreate = await commandService.CreateGatePassAsync(new GatePassCreateDto { ServiceOrderId = 0 });
        Assert.Equal(GatePassCommandStatus.BadRequest, invalidCreate.Status);
        Assert.Equal("Thiếu ServiceOrderId hợp lệ để tạo phiếu xuất cổng.", invalidCreate.ErrorMessage);

        var missingSoCreate = await commandService.CreateGatePassAsync(new GatePassCreateDto
        {
            ServiceOrderId = 999,
            ClearanceType = "NORMAL",
            QrCode = "GP-999",
            IssuedBy = 200,
            Status = "ISSUED"
        });
        Assert.Equal(GatePassCommandStatus.BadRequest, missingSoCreate.Status);
        Assert.Equal("Không tìm thấy service order để xử lý phiếu xuất cổng.", missingSoCreate.ErrorMessage);

        var closedSoCreate = await commandService.CreateGatePassAsync(new GatePassCreateDto
        {
            ServiceOrderId = 11,
            ClearanceType = "NORMAL",
            QrCode = "GP-11",
            IssuedBy = 200,
            Status = "ISSUED"
        });
        Assert.Equal(GatePassCommandStatus.BadRequest, closedSoCreate.Status);
        Assert.Equal("Không thể chỉnh sửa phiếu xuất cổng vì service order đã đóng.", closedSoCreate.ErrorMessage);

        var created = await commandService.CreateGatePassAsync(new GatePassCreateDto
        {
            ServiceOrderId = 10,
            ClearanceType = "NORMAL",
            QrCode = "GP-10",
            IssuedBy = 200,
            Status = "ISSUED"
        });
        Assert.Equal(GatePassCommandStatus.Ok, created.Status);
        Assert.NotNull(created.Data);
        Assert.Equal(10, created.Data!.ServiceOrderId);

        var missingUpdate = await commandService.UpdateGatePassAsync(9999, new GatePassUpdateDto
        {
            ServiceOrderId = 10,
            ClearanceType = "NORMAL",
            QrCode = "GP-X",
            IssuedBy = 200,
            Status = "ISSUED"
        });
        Assert.Equal(GatePassCommandStatus.NotFound, missingUpdate.Status);

        var updated = await commandService.UpdateGatePassAsync(created.Data.Id, new GatePassUpdateDto
        {
            ServiceOrderId = 0, // falls back to existing ServiceOrderId=10
            ClearanceType = "PARTIAL",
            QrCode = "GP-10-UPDATED",
            IssuedBy = 200,
            Status = "ISSUED",
            ResolutionNotes = "Đã cập nhật ghi chú"
        });
        Assert.Equal(GatePassCommandStatus.Ok, updated.Status);
        Assert.Equal(10, updated.Data!.ServiceOrderId);
        Assert.Equal("GP-10-UPDATED", updated.Data.QrCode);

        // Lock via PassedAt
        var tracked = await db.GatePasses.FindAsync(created.Data.Id);
        tracked!.PassedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var passedUpdate = await commandService.UpdateGatePassAsync(created.Data.Id, new GatePassUpdateDto
        {
            ServiceOrderId = 10,
            ClearanceType = "NORMAL",
            QrCode = "GP-10",
            IssuedBy = 200,
            Status = "ISSUED"
        });
        Assert.Equal(GatePassCommandStatus.BadRequest, passedUpdate.Status);
        Assert.Equal("Không thể chỉnh sửa phiếu xuất cổng vì xe đã được xác nhận ra cổng.", passedUpdate.ErrorMessage);

        var passedDelete = await commandService.DeleteGatePassAsync(created.Data.Id);
        Assert.Equal(GatePassCommandStatus.BadRequest, passedDelete.Status);
        Assert.Equal("Không thể chỉnh sửa phiếu xuất cổng vì xe đã được xác nhận ra cổng.", passedDelete.ErrorMessage);

        // Lock via SCANNED status (PassedAt null)
        tracked.PassedAt = null;
        tracked.Status = nameof(GatePassStatusCode.SCANNED);
        await db.SaveChangesAsync();

        var scannedDelete = await commandService.DeleteGatePassAsync(created.Data.Id);
        Assert.Equal(GatePassCommandStatus.BadRequest, scannedDelete.Status);
        Assert.Equal("Không thể chỉnh sửa phiếu xuất cổng vì xe đã được xác nhận ra cổng.", scannedDelete.ErrorMessage);

        // Unlock and verify Delete NoContent + NotFound
        tracked.Status = nameof(GatePassStatusCode.ISSUED);
        await db.SaveChangesAsync();

        var deleted = await commandService.DeleteGatePassAsync(created.Data.Id);
        Assert.Equal(GatePassCommandStatus.NoContent, deleted.Status);

        var missingDelete = await commandService.DeleteGatePassAsync(created.Data.Id);
        Assert.Equal(GatePassCommandStatus.NotFound, missingDelete.Status);
    }

    // =========================================================================
    // 2. DiagnosisRecommendation Domain Rules & CommandService
    // =========================================================================

    [Theory]
    [InlineData(null, 5L, "PART", true)]
    [InlineData(null, null, "SERVICE", true)]
    [InlineData(null, null, "standard_service", true)]
    [InlineData(null, null, " labor ", true)]
    [InlineData(10L, 5L, "SERVICE", false)] // child recommendation exempt
    [InlineData(null, null, "PART", false)]  // standalone part exempt
    public void DiagnosisRecommendationDomainRules_RequiresEvidenceForServiceRecommendation_EvaluatesRules(
        long? parentRecommendationId,
        long? standardServiceId,
        string? itemType,
        bool expectedRequired)
    {
        var rec = new DiagnosisRecommendation
        {
            ParentRecommendationId = parentRecommendationId,
            StandardServiceId = standardServiceId,
            ItemType = itemType ?? string.Empty
        };

        Assert.Equal(expectedRequired, DiagnosisRecommendationDomainRules.RequiresEvidenceForServiceRecommendation(rec));
    }

    [Theory]
    [InlineData("https://cdn.example.com/1.jpg, https://cdn.example.com/2.jpg", true)]
    [InlineData("https://cdn.example.com/1.jpg;https://cdn.example.com/2.jpg", true)]
    [InlineData("[\"https://cdn.example.com/1.jpg\"]", true)]
    [InlineData("[\"\", \"https://cdn.example.com/2.jpg\"]", true)]
    [InlineData("[]", false)]
    [InlineData("[\"   \", \"\"]", false)]
    [InlineData("[123, true, {}]", false)]
    [InlineData(" , ; \n ", false)]
    [InlineData("[broken-json", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void DiagnosisRecommendationDomainRules_HasEvidenceMedia_ParsesDelimitedAndJsonArrays(
        string? evidenceMediaUrls,
        bool expectedHasEvidence)
    {
        Assert.Equal(expectedHasEvidence, DiagnosisRecommendationDomainRules.HasEvidenceMedia(evidenceMediaUrls));
    }

    [Fact]
    public async Task DiagnosisRecommendationCommandService_ValidatesEvidenceNotifiesStaffSyncsBomAndSoftDeletes()
    {
        await using var db = CreateInMemoryContext("DiagRecCmd");
        db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "TOY", Name = "Toyota" });
        db.VehicleModels.Add(new VehicleModel { Id = 10, BrandId = 1, ModelName = "Camry" });
        db.Vehicles.Add(new Vehicle { Id = 20, LicensePlate = "30A-12345", ModelId = 10 });
        db.StandardServices.Add(new StandardService { Id = 30, Code = "SRV-OIL", Name = "Thay dầu", CategoryId = 1, BasePrice = 300_000m, StandardMinutes = 30 });
        db.Products.Add(new Product { Id = 40, Sku = "OIL-1", Name = "Dầu nhớt", Unit = "Lít", CostPrice = 100_000m });
        db.ServiceBoms.Add(new ServiceBom
        {
            Id = 50,
            ServiceId = 30,
            VehicleModelId = 10,
            ProductId = 40,
            Quantity = 2.5m,
            Unit = "Lít",
            IsMandatory = true,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow
        });
        db.ServiceOrders.Add(new ServiceOrder
        {
            Id = 100,
            Code = "SO-100",
            CustomerId = 100,
            VehicleId = 20,
            OriginBranchId = 1,
            ExecuteBranchId = 1,
            ReceptionistId = 201,
            ServiceAdvisorId = 202,
            Status = "DIAGNOSING"
        });
        db.Diagnoses.Add(new Diagnosis { Id = 200, ServiceOrderId = 100, Status = "PENDING" });
        db.DiagnosisAssignments.Add(new DiagnosisAssignment
        {
            Id = 300,
            DiagnosisId = 200,
            EventType = "ASSIGNED",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var mapper = CreateMapper();
        var recService = new DiagnosisRecommendationService(new DiagnosisRecommendationRepository(db), mapper);
        var dataService = new EfDiagnosisRecommendationDataService(db);
        var fakeOrderService = new StubServiceOrderService();
        var fakeNotifier = new RecordingDiagnosisRealtimeNotifier();
        var commandService = new DiagnosisRecommendationCommandService(
            recService,
            fakeOrderService,
            dataService,
            fakeNotifier);

        // Rejects service recommendation without evidence
        var noEvidence = await commandService.CreateAsync(new DiagnosisRecommendationCreateDto
        {
            DiagnosisId = 200,
            ItemType = "SERVICE",
            StandardServiceId = 30,
            Quantity = 1m,
            EvidenceMediaUrls = "[]",
            CreatedBy = 201 // actor should be excluded from notifications on valid create
        });
        Assert.Equal(DiagnosisRecommendationCommandStatus.BadRequest, noEvidence.Status);
        Assert.Equal(DiagnosisRecommendationDomainRules.EvidenceRequiredMessage, noEvidence.ErrorMessage);

        // Creates service recommendation with valid evidence, sets ACTIVE and resolves assignment
        var createdService = await commandService.CreateAsync(new DiagnosisRecommendationCreateDto
        {
            DiagnosisId = 200,
            ItemType = "SERVICE",
            StandardServiceId = 30,
            Quantity = 1m,
            EvidenceMediaUrls = "[\"https://cdn.example.com/issue.jpg\"]",
            CreatedBy = 201
        });
        Assert.Equal(DiagnosisRecommendationCommandStatus.Ok, createdService.Status);
        Assert.Equal(200, createdService.Data!.DiagnosisId);
        Assert.Equal(300, createdService.Data.DiagnosisAssignmentId);
        Assert.Equal(DiagnosisRecommendationDomainRules.StatusActive, createdService.Data.RecommendationStatus);

        // Notification sent only to ServiceAdvisor (202), excluding actor Receptionist (201)
        var createdEvt = Assert.Single(fakeNotifier.Events);
        Assert.Equal("CREATED", createdEvt.Event.Action);
        Assert.Equal(new[] { 202L }, createdEvt.Recipients.OrderBy(x => x).ToArray());

        // Child sub-recommendation is exempt from evidence requirement
        var createdChild = await commandService.CreateAsync(new DiagnosisRecommendationCreateDto
        {
            DiagnosisAssignmentId = 300,
            ParentRecommendationId = createdService.Data.Id,
            ItemType = "PART",
            InventoryItemId = 40,
            Quantity = 2.5m,
            EvidenceMediaUrls = null
        });
        Assert.Equal(DiagnosisRecommendationCommandStatus.Ok, createdChild.Status);

        // Updating parent quantity syncs child BOM quantity (2.5 * 2 = 5.000)
        var updatedParent = await commandService.UpdateAsync(createdService.Data.Id, new DiagnosisRecommendationUpdateDto
        {
            ItemType = "SERVICE",
            StandardServiceId = 30,
            Quantity = 2m,
            EvidenceMediaUrls = "https://cdn.example.com/updated.jpg",
            UpdatedBy = 202
        });
        Assert.Equal(DiagnosisRecommendationCommandStatus.Ok, updatedParent.Status);

        var childInDb = await db.DiagnosisRecommendations.AsNoTracking().FirstAsync(x => x.Id == createdChild.Data!.Id);
        Assert.Equal(5.0m, childInDb.Quantity);

        // Soft-delete sets SOFT_DELETED_BY_TL, SoftDeletedAt, SoftDeletedBy
        var deleted = await commandService.DeleteAsync(createdService.Data.Id);
        Assert.Equal(DiagnosisRecommendationCommandStatus.NoContent, deleted.Status);

        var deletedInDb = await db.DiagnosisRecommendations.AsNoTracking().FirstAsync(x => x.Id == createdService.Data.Id);
        Assert.Equal(nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL), deletedInDb.RecommendationStatus);
        Assert.NotNull(deletedInDb.SoftDeletedAt);
        Assert.Equal(202L, deletedInDb.SoftDeletedBy);
    }

    // =========================================================================
    // 3. ServiceOrderDetail Domain Rules & CommandService
    // =========================================================================

    [Fact]
    public void ServiceOrderDetailDomainRules_EnforcesSupplementaryAndAppliedApprovedInvariants()
    {
        // Rejects direct supplementary creation via flag or source
        Assert.Throws<InvalidOperationException>(() =>
            ServiceOrderDetailDomainRules.EnsureNoDirectSupplementaryCreate(new ServiceOrderDetail
            {
                IsSupplementary = true,
                ServiceSource = nameof(ServiceOrderDetailServiceSourceCode.INITIAL)
            }));

        Assert.Throws<InvalidOperationException>(() =>
            ServiceOrderDetailDomainRules.EnsureNoDirectSupplementaryCreate(new ServiceOrderDetail
            {
                IsSupplementary = false,
                ServiceSource = " supplementary_technician "
            }));

        Assert.Throws<InvalidOperationException>(() =>
            ServiceOrderDetailDomainRules.EnsureNoDirectSupplementaryCreate(new ServiceOrderDetail
            {
                IsSupplementary = false,
                ServiceSource = nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER)
            }));

        ServiceOrderDetailDomainRules.EnsureNoDirectSupplementaryCreate(new ServiceOrderDetail
        {
            IsSupplementary = false,
            ServiceSource = nameof(ServiceOrderDetailServiceSourceCode.INITIAL)
        });

        // Rejects mutation for applied quotation-linked or APPROVED + SourceQuotationId details
        Assert.Throws<InvalidOperationException>(() =>
            ServiceOrderDetailDomainRules.EnsureNoMutationForAppliedApprovedDetail(new ServiceOrderDetail
            {
                QuotationDetailId = 55,
                Status = nameof(ServiceOrderDetailStatusCode.QUOTING)
            }));

        Assert.Throws<InvalidOperationException>(() =>
            ServiceOrderDetailDomainRules.EnsureNoMutationForAppliedApprovedDetail(new ServiceOrderDetail
            {
                QuotationDetailId = null,
                SourceQuotationId = 12,
                Status = " approved "
            }));

        ServiceOrderDetailDomainRules.EnsureNoMutationForAppliedApprovedDetail(new ServiceOrderDetail
        {
            QuotationDetailId = null,
            SourceQuotationId = 12,
            Status = nameof(ServiceOrderDetailStatusCode.QUOTING)
        });

        // Captures normalized snapshot and restores after mutation
        var approvedAt = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
        var requestedAt = new DateTime(2026, 11, 10, 8, 30, 0, DateTimeKind.Utc);
        var entity = new ServiceOrderDetail
        {
            ServiceSource = " supplementary_technician ",
            IsSupplementary = true,
            SupplementaryReasonCode = " tech_finding ",
            SourceQuotationId = 77,
            SupplementaryRequestedByStaffId = 205,
            SupplementaryRequestedAt = requestedAt,
            CustomerApprovedAt = approvedAt,
            QuotationDetailId = 88
        };

        var snapshot = ServiceOrderDetailDomainRules.CaptureSourceSnapshot(entity);
        entity.ServiceSource = "INITIAL";
        entity.IsSupplementary = false;
        entity.SupplementaryReasonCode = "CUSTOMER_REQUEST";
        entity.SourceQuotationId = null;
        entity.SupplementaryRequestedByStaffId = null;
        entity.SupplementaryRequestedAt = null;
        entity.CustomerApprovedAt = null;
        entity.QuotationDetailId = null;

        ServiceOrderDetailDomainRules.RestoreSourceSnapshot(entity, snapshot);

        Assert.Equal(nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN), entity.ServiceSource);
        Assert.True(entity.IsSupplementary);
        Assert.Equal(nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING), entity.SupplementaryReasonCode);
        Assert.Equal(77L, entity.SourceQuotationId);
        Assert.Equal(205L, entity.SupplementaryRequestedByStaffId);
        Assert.Equal(requestedAt, entity.SupplementaryRequestedAt);
        Assert.Equal(approvedAt, entity.CustomerApprovedAt);
        Assert.Equal(88L, entity.QuotationDetailId);
    }

    [Fact]
    public async Task ServiceOrderDetailCommandService_EnforcesCreateUpdateDeleteRulesAndPreservesSnapshot()
    {
        await using var db = CreateInMemoryContext("SodCmd");
        db.ServiceOrders.Add(new ServiceOrder
        {
            Id = 500,
            Code = "SO-500",
            CustomerId = 100,
            VehicleId = 1,
            OriginBranchId = 1,
            ExecuteBranchId = 1,
            Status = "QUOTING"
        });
        db.Quotations.Add(new Quotation
        {
            Id = 600,
            ServiceOrderId = 500,
            Code = "Q-600",
            QuotationType = "STANDARD",
            InsuranceChangeType = "NONE",
            VersionNo = 2,
            Status = "CUSTOMER_APPROVED",
            CreatedBy = 200,
            CreatedAt = DateTime.UtcNow
        });
        db.QuotationDetails.Add(new QuotationDetail
        {
            Id = 601,
            QuotationId = 600,
            ServiceOrderId = 500,
            ItemType = "SERVICE",
            ItemName = "Thay má phanh",
            Quantity = 1m,
            UnitPriceAtTime = 500_000m,
            SubTotal = 500_000m,
            FinalAmount = 500_000m,
            PayerType = "CUSTOMER",
            ApprovalStatus = "APPROVED",
            Status = "APPROVED"
        });
        db.ServiceOrderDetails.AddRange(
            new ServiceOrderDetail
            {
                Id = 701,
                ServiceOrderId = 500,
                ItemType = "SERVICE",
                ItemName = "Kiểm tra hệ thống lái",
                Unit = "Lần",
                Quantity = 1m,
                UnitPriceAtTime = 150_000m,
                SubTotal = 150_000m,
                TaxRate = 0m,
                TaxAmount = 0m,
                FinalAmount = 150_000m,
                PayerType = "CUSTOMER",
                Status = "QUOTING",
                ServiceSource = "INITIAL",
                IsSupplementary = false,
                SourceQuotationId = null
            },
            new ServiceOrderDetail
            {
                Id = 702,
                ServiceOrderId = 500,
                QuotationDetailId = 601,
                SourceQuotationId = 600,
                ItemType = "SERVICE",
                ItemName = "Thay má phanh",
                Unit = "Bộ",
                Quantity = 1m,
                UnitPriceAtTime = 500_000m,
                SubTotal = 500_000m,
                TaxRate = 0m,
                TaxAmount = 0m,
                FinalAmount = 500_000m,
                PayerType = "CUSTOMER",
                Status = "APPROVED",
                ServiceSource = "SUPPLEMENTARY_TECHNICIAN",
                IsSupplementary = true,
                SupplementaryReasonCode = "TECH_FINDING"
            });
        await db.SaveChangesAsync();

        var mapper = CreateMapper();
        var detailService = new ServiceOrderDetailService(
            new ServiceOrderDetailRepository(db),
            new EfServiceOrderDetailStore(db),
            mapper);
        var fakeOrderService = new StubServiceOrderService();
        var commandService = new ServiceOrderDetailCommandService(detailService, fakeOrderService);

        // Rejects direct supplementary creation
        var directSupp = await commandService.CreateAsync(new ServiceOrderDetailCreateDto
        {
            ServiceOrderId = 500,
            ItemType = "SERVICE",
            ItemName = "Phát sinh trực tiếp",
            Unit = "Lần",
            Quantity = 1m,
            UnitPriceAtTime = 100_000m,
            SubTotal = 100_000m,
            FinalAmount = 100_000m,
            Status = "QUOTING",
            IsSupplementary = true,
            ServiceSource = "SUPPLEMENTARY_TECHNICIAN"
        });
        Assert.Equal(ServiceOrderDetailCommandStatus.BadRequest, directSupp.Status);
        Assert.Equal(ServiceOrderDetailDomainRules.DirectSupplementaryCreateForbiddenMessage, directSupp.ErrorMessage);

        // Update editable detail preserves SourceSnapshot even if DTO attempts tampering
        var updatedEditable = await commandService.UpdateAsync(701, new ServiceOrderDetailUpdateDto
        {
            ServiceOrderId = 0, // falls back to 500
            ItemType = "SERVICE",
            ItemName = "Kiểm tra hệ thống lái (cập nhật)",
            Unit = "Lần",
            Quantity = 1m,
            UnitPriceAtTime = 180_000m,
            SubTotal = 180_000m,
            FinalAmount = 180_000m,
            Status = "QUOTING",
            ServiceSource = "SUPPLEMENTARY_CUSTOMER",
            IsSupplementary = true,
            SupplementaryReasonCode = "CUSTOMER_REQUEST",
            SourceQuotationId = 999
        });
        Assert.Equal(ServiceOrderDetailCommandStatus.Ok, updatedEditable.Status);
        Assert.Equal(500L, updatedEditable.Data!.ServiceOrderId);
        Assert.Equal("INITIAL", updatedEditable.Data.ServiceSource);
        Assert.False(updatedEditable.Data.IsSupplementary);
        Assert.Null(updatedEditable.Data.SupplementaryReasonCode);
        Assert.Null(updatedEditable.Data.SourceQuotationId);

        // Rejects updating and deleting applied quotation-linked approved detail
        var appliedUpdate = await commandService.UpdateAsync(702, new ServiceOrderDetailUpdateDto
        {
            ServiceOrderId = 500,
            ItemName = "Cố sửa hạng mục đã duyệt"
        });
        Assert.Equal(ServiceOrderDetailCommandStatus.BadRequest, appliedUpdate.Status);
        Assert.Equal(ServiceOrderDetailDomainRules.AppliedApprovedDetailMutationForbiddenMessage, appliedUpdate.ErrorMessage);

        var appliedDelete = await commandService.DeleteAsync(702);
        Assert.Equal(ServiceOrderDetailCommandStatus.BadRequest, appliedDelete.Status);
        Assert.Equal(ServiceOrderDetailDomainRules.AppliedApprovedDetailMutationForbiddenMessage, appliedDelete.ErrorMessage);

        // Delete editable detail succeeds
        var deletedEditable = await commandService.DeleteAsync(701);
        Assert.Equal(ServiceOrderDetailCommandStatus.NoContent, deletedEditable.Status);
        Assert.Null(await commandService.GetByIdAsync(701));
    }

    // =========================================================================
    // 4. End-to-End HTTP Contracts for All 3 Refactored Workshop Controllers
    // =========================================================================

    [Fact]
    public async Task RefactoredWorkshopControllers_PreserveHttpRoutesAndProblemDetailsContracts()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "TOY", Name = "Toyota" });
            db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "Vios" });
            db.Vehicles.Add(new Vehicle { Id = 1, LicensePlate = "51A-77777", ModelId = 1 });
            db.StandardServices.Add(new StandardService { Id = 30, Code = "SRV-1", Name = "Bảo dưỡng", CategoryId = 1, BasePrice = 200_000m, StandardMinutes = 30 });
            db.ServiceOrders.AddRange(
                new ServiceOrder { Id = 8001, Code = "SO-8001", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "DIAGNOSING" },
                new ServiceOrder { Id = 8002, Code = "SO-8002", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "CLOSED" },
                new ServiceOrder { Id = 8003, Code = "SO-8003", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "DIAGNOSING" });
            db.Diagnoses.Add(new Diagnosis { Id = 8101, ServiceOrderId = 8003, Status = "PENDING" });
            db.DiagnosisAssignments.Add(new DiagnosisAssignment { Id = 8201, DiagnosisId = 8101, EventType = "ASSIGNED", CreatedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        // 1. GatePassController
        using var gpClosedRes = await client.PostAsJsonAsync("/api/v1/gate-passes", new
        {
            serviceOrderId = 8002,
            clearanceType = "NORMAL",
            qrCode = "GP-CLOSED",
            issuedBy = 200,
            status = "ISSUED"
        });
        Assert.Equal(HttpStatusCode.BadRequest, gpClosedRes.StatusCode);
        using var gpClosedJson = JsonDocument.Parse(await gpClosedRes.Content.ReadAsStringAsync());
        Assert.Equal("Không thể chỉnh sửa phiếu xuất cổng vì service order đã đóng.", gpClosedJson.RootElement.GetProperty("detail").GetString());

        using var gpCreateRes = await client.PostAsJsonAsync("/api/v1/gate-passes", new
        {
            serviceOrderId = 8001,
            clearanceType = "NORMAL",
            qrCode = "GP-8001",
            issuedBy = 200,
            status = "ISSUED"
        });
        Assert.Equal(HttpStatusCode.Created, gpCreateRes.StatusCode);
        using var gpCreatedJson = JsonDocument.Parse(await gpCreateRes.Content.ReadAsStringAsync());
        var gpId = gpCreatedJson.RootElement.GetProperty("id").GetInt64();

        using var gpGetRes = await client.GetAsync($"/api/v1/gate-passes/{gpId}");
        Assert.Equal(HttpStatusCode.OK, gpGetRes.StatusCode);

        using var gpScanRes = await client.PutAsJsonAsync($"/api/v1/gate-passes/{gpId}", new
        {
            serviceOrderId = 8001,
            clearanceType = "NORMAL",
            qrCode = "GP-8001",
            issuedBy = 200,
            status = "SCANNED"
        });
        Assert.Equal(HttpStatusCode.BadRequest, gpScanRes.StatusCode);
        using (var scanScope = factory.Services.CreateScope())
        {
            var scanDb = scanScope.ServiceProvider.GetRequiredService<MyGarage.Data.GarageContext>();
            var gatePass = await scanDb.GatePasses.FindAsync(gpId);
            gatePass!.Status = "SCANNED";
            await scanDb.SaveChangesAsync();
        }

        using var gpLockedUpdateRes = await client.PutAsJsonAsync($"/api/v1/gate-passes/{gpId}", new
        {
            serviceOrderId = 8001,
            clearanceType = "NORMAL",
            qrCode = "GP-8001-EDIT",
            issuedBy = 200,
            status = "ISSUED"
        });
        Assert.Equal(HttpStatusCode.BadRequest, gpLockedUpdateRes.StatusCode);
        using var gpLockedJson = JsonDocument.Parse(await gpLockedUpdateRes.Content.ReadAsStringAsync());
        Assert.Equal("Không thể chỉnh sửa phiếu xuất cổng vì xe đã được xác nhận ra cổng.", gpLockedJson.RootElement.GetProperty("detail").GetString());

        // 2. DiagnosisRecommendationController (both dual routes)
        using var drMissingEvidenceRes = await client.PostAsJsonAsync("/api/v1/diagnose-recommendations", new
        {
            diagnosisId = 8101,
            itemType = "SERVICE",
            standardServiceId = 30,
            quantity = 1,
            evidenceMediaUrls = "[]"
        });
        Assert.Equal(HttpStatusCode.BadRequest, drMissingEvidenceRes.StatusCode);
        using var drMissingJson = JsonDocument.Parse(await drMissingEvidenceRes.Content.ReadAsStringAsync());
        Assert.Equal(DiagnosisRecommendationDomainRules.EvidenceRequiredMessage, drMissingJson.RootElement.GetProperty("detail").GetString());

        using var drCreateRes = await client.PostAsJsonAsync("/api/v1/diagnosis-recommendations", new
        {
            diagnosisId = 8101,
            itemType = "SERVICE",
            standardServiceId = 30,
            quantity = 1,
            evidenceMediaUrls = "[\"https://cdn.example.com/ok.jpg\"]"
        });
        Assert.Equal(HttpStatusCode.Created, drCreateRes.StatusCode);
        using var drCreatedJson = JsonDocument.Parse(await drCreateRes.Content.ReadAsStringAsync());
        var recId = drCreatedJson.RootElement.GetProperty("id").GetInt64();

        using var drGetRes = await client.GetAsync($"/api/v1/diagnosis-recommendations/{recId}");
        Assert.Equal(HttpStatusCode.OK, drGetRes.StatusCode);

        using var drDeleteRes = await client.DeleteAsync($"/api/v1/diagnosis-recommendations/{recId}");
        Assert.Equal(HttpStatusCode.NoContent, drDeleteRes.StatusCode);

        // 3. ServiceOrderDetailController
        using var sodSuppRes = await client.PostAsJsonAsync("/api/v1/service-order-details", new
        {
            serviceOrderId = 8003,
            itemType = "SERVICE",
            itemName = "Phát sinh",
            unit = "Lần",
            quantity = 1,
            unitPriceAtTime = 100000,
            subTotal = 100000,
            finalAmount = 100000,
            status = "QUOTING",
            isSupplementary = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, sodSuppRes.StatusCode);
        using var sodSuppJson = JsonDocument.Parse(await sodSuppRes.Content.ReadAsStringAsync());
        Assert.Equal(ServiceOrderDetailDomainRules.DirectSupplementaryCreateForbiddenMessage, sodSuppJson.RootElement.GetProperty("detail").GetString());

        using var sodCreateRes = await client.PostAsJsonAsync("/api/v1/service-order-details", new
        {
            serviceOrderId = 8003,
            itemType = "SERVICE",
            itemName = "Thay lọc gió",
            unit = "Lần",
            quantity = 1,
            unitPriceAtTime = 120000,
            subTotal = 120000,
            finalAmount = 120000,
            status = "QUOTING",
            serviceSource = "INITIAL",
            isSupplementary = false
        });
        Assert.Equal(HttpStatusCode.Created, sodCreateRes.StatusCode);
        using var sodCreatedJson = JsonDocument.Parse(await sodCreateRes.Content.ReadAsStringAsync());
        var sodId = sodCreatedJson.RootElement.GetProperty("id").GetInt64();

        using var sodUpdateRes = await client.PutAsJsonAsync($"/api/v1/service-order-details/{sodId}", new
        {
            serviceOrderId = 8003,
            itemType = "SERVICE",
            itemName = "Thay lọc gió (đã sửa)",
            unit = "Lần",
            quantity = 1,
            unitPriceAtTime = 130000,
            subTotal = 130000,
            finalAmount = 130000,
            status = "QUOTING",
            serviceSource = "SUPPLEMENTARY_CUSTOMER",
            isSupplementary = true
        });
        Assert.Equal(HttpStatusCode.OK, sodUpdateRes.StatusCode);
        using var sodUpdatedJson = JsonDocument.Parse(await sodUpdateRes.Content.ReadAsStringAsync());
        Assert.Equal("INITIAL", sodUpdatedJson.RootElement.GetProperty("serviceSource").GetString());
        Assert.False(sodUpdatedJson.RootElement.GetProperty("isSupplementary").GetBoolean());

        using var sodDeleteRes = await client.DeleteAsync($"/api/v1/service-order-details/{sodId}");
        Assert.Equal(HttpStatusCode.NoContent, sodDeleteRes.StatusCode);
    }

    private sealed class StubServiceOrderService : IServiceOrderService
    {
        public Dictionary<long, ServiceOrderEditCapabilityResponseDto> CapabilitiesById { get; } = new();
        public InvalidOperationException? WorkshopException { get; set; }
        public InvalidOperationException? QuoteAndPartsException { get; set; }

        public Task<ServiceOrderEditCapabilityResponseDto?> GetEditCapabilityAsync(long id)
            => Task.FromResult(CapabilitiesById.TryGetValue(id, out var capability) ? capability : null);

        public Task EnsureWorkshopEditableAsync(long id)
        {
            if (WorkshopException != null)
            {
                throw WorkshopException;
            }
            return Task.CompletedTask;
        }

        public Task EnsureQuoteAndPartsEditableAsync(long id)
        {
            if (QuoteAndPartsException != null)
            {
                throw QuoteAndPartsException;
            }
            return Task.CompletedTask;
        }

        public Task EnsureAssignmentEditableAsync(long id) => Task.CompletedTask;
        public Task EnsurePartsEditableAsync(long id) => Task.CompletedTask;
        public Task<ServiceOrderBatchCreateResult> AddWorkshopBatchAsync(long? actorUserId, ServiceOrderBatchCreateRequestDto dto) => throw new NotImplementedException();
        public Task<ServiceOrder?> UpdateWorkshopAsync(long id, WorkshopServiceOrderEditRequestDto dto, long? actorUserId = null) => throw new NotImplementedException();
        public Task<HandoverReadinessResponseDto?> GetHandoverReadinessAsync(long id) => throw new NotImplementedException();
        public Task<bool> ProcessHandoverAsync(long id, HandoverRequestDto request) => throw new NotImplementedException();
        public Task<IEnumerable<ServiceOrder>> GetAllAsync() => throw new NotImplementedException();
        public Task<(IReadOnlyList<ServiceOrder> Items, int Total)> GetPageByIdAsync(int page, int pageSize) => throw new NotImplementedException();
        public Task<ServiceOrder?> GetByIdAsync(object id) => throw new NotImplementedException();
        public Task<ServiceOrder> AddAsync(ServiceOrder entity) => throw new NotImplementedException();
        public Task<ServiceOrder?> UpdateAsync(object id, ServiceOrder entity) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(object id) => throw new NotImplementedException();
    }

    private sealed class RecordingDiagnosisRealtimeNotifier : IDiagnosisRealtimeNotifier
    {
        public List<(IReadOnlyCollection<long> Recipients, SupplementaryProposalRealtimeEvent Event)> Events { get; } = new();

        public Task NotifySupplementaryProposalUpdatedAsync(
            IReadOnlyCollection<long> recipientStaffIds,
            SupplementaryProposalRealtimeEvent evt,
            CancellationToken ct = default)
        {
            Events.Add((recipientStaffIds.ToArray(), evt));
            return Task.CompletedTask;
        }
    }
}
