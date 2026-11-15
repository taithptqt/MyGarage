using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Payroll;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class Batch4BPayrollAndQcTemplatesTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PayrollRunsApplicationService_PreviewAsync_HandlesValidDefaultAndInvalidPeriods()
    {
        var fakeDataService = new FakePayrollRunDataService
        {
            PreviewResultFactory = (branchId, periodCode, _, _) => new PayrollRunPreviewResult(
                periodCode,
                branchId,
                "DRAFT",
                12_500_000m,
                new List<PayrollRunPreviewDetail>
                {
                    new(
                        StaffId: 10,
                        StaffName: "Kỹ thuật viên A",
                        BaseSalary: 10_000_000m,
                        AllowanceAmount: 1_000_000m,
                        CommissionAmount: 1_500_000m,
                        OvertimeAmount: 500_000m,
                        Bonus: 200_000m,
                        Deductions: 500_000m,
                        Penalty: 200_000m,
                        NetSalary: 12_500_000m)
                })
        };

        var service = new PayrollRunsApplicationService(fakeDataService);

        var validResult = await service.PreviewAsync(2, " 2026-05 ");
        Assert.Equal(PayrollRunEndpointActionStatus.Ok, validResult.Status);
        Assert.NotNull(validResult.Data);
        Assert.Equal("2026-05", validResult.Data!.Period);
        Assert.Equal(2, validResult.Data.BranchId);
        Assert.Equal(new DateOnly(2026, 5, 1), fakeDataService.LastStartDate);
        Assert.Equal(new DateOnly(2026, 5, 31), fakeDataService.LastEndDate);
        Assert.Equal(12_500_000m, validResult.Data.TotalAmount);
        var previewDetail = Assert.Single(validResult.Data.Details);
        Assert.Equal(10, previewDetail.StaffId);
        Assert.Equal("Kỹ thuật viên A", previewDetail.StaffName);
        Assert.Equal(12_500_000m, previewDetail.NetSalary);

        var previewJson = JsonSerializer.Serialize(validResult.Data, WebJsonOptions);
        using var previewDoc = JsonDocument.Parse(previewJson);
        var previewRoot = previewDoc.RootElement;
        Assert.Equal("2026-05", previewRoot.GetProperty("period").GetString());
        Assert.Equal(2, previewRoot.GetProperty("branchId").GetInt64());
        Assert.Equal("DRAFT", previewRoot.GetProperty("status").GetString());
        Assert.Equal(12_500_000m, previewRoot.GetProperty("totalAmount").GetDecimal());
        var previewItemJson = previewRoot.GetProperty("details")[0];
        Assert.Equal(10, previewItemJson.GetProperty("staffId").GetInt64());
        Assert.Equal("Kỹ thuật viên A", previewItemJson.GetProperty("staffName").GetString());
        Assert.Equal(10_000_000m, previewItemJson.GetProperty("baseSalary").GetDecimal());
        Assert.Equal(1_000_000m, previewItemJson.GetProperty("allowanceAmount").GetDecimal());
        Assert.Equal(1_500_000m, previewItemJson.GetProperty("commissionAmount").GetDecimal());
        Assert.Equal(500_000m, previewItemJson.GetProperty("overtimeAmount").GetDecimal());
        Assert.Equal(200_000m, previewItemJson.GetProperty("bonus").GetDecimal());
        Assert.Equal(500_000m, previewItemJson.GetProperty("deductions").GetDecimal());
        Assert.Equal(200_000m, previewItemJson.GetProperty("penalty").GetDecimal());
        Assert.Equal(12_500_000m, previewItemJson.GetProperty("netSalary").GetDecimal());

        Assert.True(PayrollDomainRules.TryParsePeriod(null, out var expectedDefaultCode, out var expectedDefaultStart, out var expectedDefaultEnd));
        var defaultResult = await service.PreviewAsync(3, "   ");
        Assert.Equal(PayrollRunEndpointActionStatus.Ok, defaultResult.Status);
        Assert.Equal(expectedDefaultCode, defaultResult.Data!.Period);
        Assert.Equal(expectedDefaultStart, fakeDataService.LastStartDate);
        Assert.Equal(expectedDefaultEnd, fakeDataService.LastEndDate);

        var callCountBeforeInvalid = fakeDataService.PreviewCallCount;
        var invalidResult = await service.PreviewAsync(2, "2026/13");
        Assert.Equal(PayrollRunEndpointActionStatus.BadRequest, invalidResult.Status);
        Assert.Equal("Định dạng period phải là yyyy-MM.", invalidResult.ErrorMessage);
        Assert.Equal(callCountBeforeInvalid, fakeDataService.PreviewCallCount);
    }

    [Fact]
    public async Task PayrollRunsApplicationService_GetAllCreateAndApprove_CalculateDeductionsTotalAmountAndDtoFields()
    {
        var createdAt = new DateTime(2026, 5, 31, 10, 0, 0, DateTimeKind.Utc);
        var calculatedAt = new DateTime(2026, 5, 31, 11, 0, 0, DateTimeKind.Utc);
        var approvedAt = new DateTime(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

        var sampleRun = new PayrollRun
        {
            Id = 55,
            PayrollPeriodId = 8,
            PayrollPeriod = new PayrollPeriod { Id = 8, PeriodCode = "2026-05", Status = "OPEN" },
            RunNo = 2,
            BranchId = 1,
            Status = "APPROVED",
            CalculatedAt = calculatedAt,
            ApprovedAt = approvedAt,
            PaidAt = null,
            CreatedAt = createdAt,
            PayrollItems = new List<PayrollItem>
            {
                new()
                {
                    Id = 101,
                    PayrollRunId = 55,
                    StaffId = 201,
                    Staff = new Staff { Id = 201, FullName = "Nguyễn Văn A", PasswordHash = "h" },
                    BaseSalary = 12_000_000m,
                    AllowanceAmount = 1_000_000m,
                    CommissionAmount = 2_000_000m,
                    OvertimeAmount = 500_000m,
                    BonusAmount = 300_000m,
                    AttendanceDeduction = 100_000m,
                    AdvanceDeduction = 200_000m,
                    LoanDeduction = 300_000m,
                    InsuranceEmployeeAmount = 1_050_000m,
                    PitAmount = 150_000m,
                    OtherDeduction = 50_000m,
                    PenaltyAmount = 75_000m,
                    NetPay = 13_875_000m,
                    Status = "APPROVED"
                },
                new()
                {
                    Id = 102,
                    PayrollRunId = 55,
                    StaffId = 202,
                    Staff = new Staff { Id = 202, FullName = "Trần Thị B", PasswordHash = "h" },
                    BaseSalary = 9_000_000m,
                    AllowanceAmount = 500_000m,
                    CommissionAmount = 0m,
                    OvertimeAmount = 0m,
                    BonusAmount = 0m,
                    AttendanceDeduction = 0m,
                    AdvanceDeduction = 0m,
                    LoanDeduction = 0m,
                    InsuranceEmployeeAmount = 945_000m,
                    PitAmount = 0m,
                    OtherDeduction = 55_000m,
                    PenaltyAmount = 0m,
                    NetPay = 8_500_000m,
                    Status = "APPROVED"
                }
            }
        };

        var fakeDataService = new FakePayrollRunDataService
        {
            PagedResult = (new List<PayrollRun> { sampleRun }, 1),
            CreateResult = new PayrollRunCreateResult(PayrollRunCreateStatus.Created, Run: sampleRun),
            ApproveResult = new PayrollRunApproveResult(PayrollRunApproveStatus.Ok, Run: sampleRun)
        };

        var service = new PayrollRunsApplicationService(fakeDataService);

        var pagedResult = await service.GetAllAsync(scopedBranchId: 1, page: 1, pageSize: 20);
        Assert.Equal(PayrollRunEndpointActionStatus.Ok, pagedResult.Status);
        Assert.NotNull(pagedResult.Data);
        Assert.Equal(1, pagedResult.Data!.TotalCount);
        var mappedRun = Assert.Single(pagedResult.Data.Items);

        Assert.Equal(55, mappedRun.Id);
        Assert.Equal(8, mappedRun.PayrollPeriodId);
        Assert.Equal("2026-05", mappedRun.Period);
        Assert.Equal(2, mappedRun.RunNo);
        Assert.Equal(1, mappedRun.BranchId);
        Assert.Equal("APPROVED", mappedRun.Status);
        Assert.Equal(22_375_000m, mappedRun.TotalAmount);
        Assert.Equal(2, mappedRun.Details.Count);

        var firstDetail = mappedRun.Details[0];
        Assert.Equal(101, firstDetail.Id);
        Assert.Equal(201, firstDetail.StaffId);
        Assert.Equal("Nguyễn Văn A", firstDetail.StaffName);
        Assert.Equal(1_850_000m, firstDetail.Deductions);
        Assert.Equal(75_000m, firstDetail.Penalty);
        Assert.Equal(13_875_000m, firstDetail.NetSalary);
        Assert.Equal(13_875_000m, firstDetail.NetPay);
        Assert.Equal(firstDetail.NetSalary, firstDetail.NetPay);
        Assert.Equal("APPROVED", firstDetail.Status);

        var runJson = JsonSerializer.Serialize(mappedRun, WebJsonOptions);
        using var runDoc = JsonDocument.Parse(runJson);
        var runRoot = runDoc.RootElement;
        Assert.Equal(55, runRoot.GetProperty("id").GetInt64());
        Assert.Equal(8, runRoot.GetProperty("payrollPeriodId").GetInt64());
        Assert.Equal("2026-05", runRoot.GetProperty("period").GetString());
        Assert.Equal(2, runRoot.GetProperty("runNo").GetInt32());
        Assert.Equal(1, runRoot.GetProperty("branchId").GetInt64());
        Assert.Equal("APPROVED", runRoot.GetProperty("status").GetString());
        Assert.True(runRoot.TryGetProperty("calculatedAt", out _));
        Assert.True(runRoot.TryGetProperty("approvedAt", out _));
        Assert.True(runRoot.TryGetProperty("paidAt", out _));
        Assert.True(runRoot.TryGetProperty("createdAt", out _));
        Assert.Equal(22_375_000m, runRoot.GetProperty("totalAmount").GetDecimal());
        var detailJson = runRoot.GetProperty("details")[0];
        Assert.Equal(1_850_000m, detailJson.GetProperty("deductions").GetDecimal());
        Assert.Equal(13_875_000m, detailJson.GetProperty("netSalary").GetDecimal());
        Assert.Equal(13_875_000m, detailJson.GetProperty("netPay").GetDecimal());

        var invalidCreate = await service.CreateAsync(1, 999, new PayrollRunCreateEndpointDto { Period = "bad-period" });
        Assert.Equal(PayrollRunEndpointActionStatus.BadRequest, invalidCreate.Status);
        Assert.Equal("Định dạng period phải là yyyy-MM.", invalidCreate.ErrorMessage);
        Assert.Equal(0, fakeDataService.CreateCallCount);

        var createdResult = await service.CreateAsync(
            1,
            null,
            new PayrollRunCreateEndpointDto
            {
                Period = "2026-05",
                BranchId = 1,
                CreatedBy = 777,
                Details = new List<PayrollRunCreateItemDto>
                {
                    new() { StaffId = 201, BaseSalary = 12_000_000m }
                }
            });
        Assert.Equal(PayrollRunEndpointActionStatus.Created, createdResult.Status);
        Assert.Equal(777, fakeDataService.LastActorId);
        Assert.Equal("2026-05", fakeDataService.LastPeriodCode);

        fakeDataService.ApproveResult = new PayrollRunApproveResult(PayrollRunApproveStatus.NotFound, "Không tìm thấy bảng lương.");
        Assert.Equal(PayrollRunEndpointActionStatus.NotFound, (await service.ApproveAsync(999, 1, 200)).Status);

        fakeDataService.ApproveResult = new PayrollRunApproveResult(PayrollRunApproveStatus.Forbidden);
        Assert.Equal(PayrollRunEndpointActionStatus.Forbidden, (await service.ApproveAsync(55, 2, 200)).Status);

        fakeDataService.ApproveResult = new PayrollRunApproveResult(PayrollRunApproveStatus.BadRequest, "Bảng lương không hợp lệ.");
        Assert.Equal(PayrollRunEndpointActionStatus.BadRequest, (await service.ApproveAsync(55, 1, 200)).Status);

        fakeDataService.ApproveResult = new PayrollRunApproveResult(PayrollRunApproveStatus.Ok, Run: sampleRun);
        var approved = await service.ApproveAsync(55, 1, 200);
        Assert.Equal(PayrollRunEndpointActionStatus.Ok, approved.Status);
        Assert.Equal(22_375_000m, approved.Data!.TotalAmount);
    }

    [Fact]
    public async Task QcChecklistTemplateCommandService_ValidatesEmptyNameAndBuildsItemsVsGroupsWithDefaults()
    {
        var fakeDataService = new FakeQcChecklistTemplateDataService();
        var service = new QcChecklistTemplateCommandService(fakeDataService);

        var nullCreate = await service.CreateAsync(null);
        Assert.Equal(QcChecklistTemplateCommandStatus.BadRequest, nullCreate.Status);
        Assert.Equal("Tên template QC là bắt buộc.", nullCreate.ErrorMessage);

        var blankCreate = await service.CreateAsync(new QcTemplateUpsertDto { Name = "   " });
        Assert.Equal(QcChecklistTemplateCommandStatus.BadRequest, blankCreate.Status);
        Assert.Equal("Tên template QC là bắt buộc.", blankCreate.ErrorMessage);

        var blankUpdate = await service.UpdateAsync(1, new QcTemplateUpsertDto { Name = "" });
        Assert.Equal(QcChecklistTemplateCommandStatus.BadRequest, blankUpdate.Status);
        Assert.Equal("Tên template QC là bắt buộc.", blankUpdate.ErrorMessage);
        Assert.Equal(0, fakeDataService.CreateCallCount);
        Assert.Equal(0, fakeDataService.UpdateCallCount);

        var fromItemsResult = await service.CreateAsync(new QcTemplateUpsertDto
        {
            Name = "  Template từ Items  ",
            Description = "  Mô tả QC  ",
            ServiceCategory = "  MAINTENANCE  ",
            IsActive = null,
            Items = new List<QcTemplateItemInputDto>
            {
                new() { Category = "   ", ItemName = null, Name = "  Kiểm tra phanh  ", IsRequired = null, SortOrder = null },
                new() { Category = "Động cơ", ItemName = "   " },
                new() { Category = "  Động cơ  ", ItemName = "  Kiểm tra nhớt  ", Description = "  Mức chuẩn  ", IsRequired = false, SortOrder = 5 }
            },
            Groups = new List<QcTemplateGroupInputDto>
            {
                new() { Name = "IgnoredGroup", Items = new List<string> { "Should be ignored because Items is non-empty" } }
            }
        });

        Assert.Equal(QcChecklistTemplateCommandStatus.Created, fromItemsResult.Status);
        Assert.NotNull(fromItemsResult.Data);
        Assert.Equal("Template từ Items", fromItemsResult.Data!.Name);
        Assert.Equal("Mô tả QC", fromItemsResult.Data.Description);
        Assert.Equal("MAINTENANCE", fromItemsResult.Data.ServiceCategory);
        Assert.True(fromItemsResult.Data.IsActive);
        Assert.Equal(2, fromItemsResult.Data.Items.Count);

        Assert.Equal("Tổng quát", fromItemsResult.Data.Items[0].Category);
        Assert.Equal("Kiểm tra phanh", fromItemsResult.Data.Items[0].ItemName);
        Assert.Equal("Kiểm tra phanh", fromItemsResult.Data.Items[0].Name);
        Assert.True(fromItemsResult.Data.Items[0].IsRequired);
        Assert.Equal(1, fromItemsResult.Data.Items[0].SortOrder);

        Assert.Equal("Động cơ", fromItemsResult.Data.Items[1].Category);
        Assert.Equal("Kiểm tra nhớt", fromItemsResult.Data.Items[1].ItemName);
        Assert.Equal("Mức chuẩn", fromItemsResult.Data.Items[1].Description);
        Assert.False(fromItemsResult.Data.Items[1].IsRequired);
        Assert.Equal(5, fromItemsResult.Data.Items[1].SortOrder);

        Assert.Equal(2, fromItemsResult.Data.Groups.Count);
        Assert.Equal("Tổng quát", fromItemsResult.Data.Groups[0].Name);
        Assert.Equal(new[] { "Kiểm tra phanh" }, fromItemsResult.Data.Groups[0].Items);

        var fromGroupsResult = await service.CreateAsync(new QcTemplateUpsertDto
        {
            Name = "Template từ Groups",
            Items = new List<QcTemplateItemInputDto>(),
            Groups = new List<QcTemplateGroupInputDto>
            {
                new() { Name = null, Items = new List<string> { "  Kiểm tra đèn  ", "   ", "Kiểm tra còi" } },
                new() { Name = "  Gầm xe  ", Items = new List<string> { "Kiểm tra lốp" } }
            }
        });

        Assert.Equal(QcChecklistTemplateCommandStatus.Created, fromGroupsResult.Status);
        Assert.NotNull(fromGroupsResult.Data);
        Assert.Equal(3, fromGroupsResult.Data!.Items.Count);
        Assert.Equal(new[] { 1, 2, 3 }, fromGroupsResult.Data.Items.Select(x => x.SortOrder).ToArray());
        Assert.Equal("Tổng quát", fromGroupsResult.Data.Items[0].Category);
        Assert.Equal("Tổng quát", fromGroupsResult.Data.Items[1].Category);
        Assert.Equal("Gầm xe", fromGroupsResult.Data.Items[2].Category);
        Assert.All(fromGroupsResult.Data.Items, x => Assert.True(x.IsRequired));

        var templateJson = JsonSerializer.Serialize(fromGroupsResult.Data, WebJsonOptions);
        using var templateDoc = JsonDocument.Parse(templateJson);
        var templateRoot = templateDoc.RootElement;
        Assert.True(templateRoot.TryGetProperty("id", out _));
        Assert.Equal("Template từ Groups", templateRoot.GetProperty("name").GetString());
        Assert.True(templateRoot.TryGetProperty("description", out _));
        Assert.True(templateRoot.TryGetProperty("serviceCategory", out _));
        Assert.True(templateRoot.GetProperty("isActive").GetBoolean());
        Assert.True(templateRoot.TryGetProperty("createdAt", out _));
        Assert.Equal(2, templateRoot.GetProperty("groups").GetArrayLength());
        var firstItemJson = templateRoot.GetProperty("items")[0];
        Assert.True(firstItemJson.TryGetProperty("id", out _));
        Assert.True(firstItemJson.TryGetProperty("templateId", out _));
        Assert.Equal("Tổng quát", firstItemJson.GetProperty("category").GetString());
        Assert.Equal("Kiểm tra đèn", firstItemJson.GetProperty("name").GetString());
        Assert.Equal("Kiểm tra đèn", firstItemJson.GetProperty("itemName").GetString());
        Assert.True(firstItemJson.GetProperty("isRequired").GetBoolean());
        Assert.Equal(1, firstItemJson.GetProperty("sortOrder").GetInt32());
    }

    [Fact]
    public async Task QcChecklistTemplateCommandService_DeleteGetAndUpdate_HandleDeactivatedDeletedAndNotFound()
    {
        var fakeDataService = new FakeQcChecklistTemplateDataService
        {
            ServiceOrderCategories = new Dictionary<long, string> { [77] = "REPAIR" }
        };
        var service = new QcChecklistTemplateCommandService(fakeDataService);

        fakeDataService.Templates.Add(new QcChecklistTemplate
        {
            Id = 10,
            Name = "QC Sửa chữa",
            ServiceCategory = "REPAIR",
            IsActive = true,
            CreatedAt = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc),
            TemplateItems = new List<QcChecklistTemplateItem>
            {
                new() { Id = 2, TemplateId = 10, Category = null, ItemName = "Mục 2", IsRequired = true, SortOrder = 2 },
                new() { Id = 1, TemplateId = 10, Category = " ", ItemName = "Mục 1", IsRequired = false, SortOrder = 1 }
            }
        });

        var envelope = await service.GetTemplatesAsync(serviceType: null, serviceOrderId: 77, includeInactive: false);
        Assert.Equal("REPAIR", envelope.ServiceType);
        Assert.Equal(1, envelope.Total);
        var fetchedTemplate = Assert.Single(envelope.Items);
        Assert.Equal(new[] { "Mục 1", "Mục 2" }, fetchedTemplate.Items.Select(x => x.ItemName).ToArray());
        Assert.Equal("Tổng quát", Assert.Single(fetchedTemplate.Groups).Name);

        var envelopeJson = JsonSerializer.Serialize(envelope, WebJsonOptions);
        using var envelopeDoc = JsonDocument.Parse(envelopeJson);
        Assert.Equal("REPAIR", envelopeDoc.RootElement.GetProperty("serviceType").GetString());
        Assert.Equal(1, envelopeDoc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, envelopeDoc.RootElement.GetProperty("items").GetArrayLength());

        var listTemplates = await service.GetTemplatesListAsync("REPAIR", includeInactive: true);
        Assert.Single(listTemplates);

        var notFoundGet = await service.GetByIdAsync(999);
        Assert.Equal(QcChecklistTemplateCommandStatus.NotFound, notFoundGet.Status);
        Assert.Equal("Không tìm thấy template QC.", notFoundGet.ErrorMessage);

        var missingUpdate = await service.UpdateAsync(999, new QcTemplateUpsertDto { Name = "Updated" });
        Assert.Equal(QcChecklistTemplateCommandStatus.NotFound, missingUpdate.Status);
        Assert.Equal("Không tìm thấy template QC.", missingUpdate.ErrorMessage);
        Assert.Null(fakeDataService.LastUpdateReplacementItems);

        var metadataOnlyUpdate = await service.UpdateAsync(10, new QcTemplateUpsertDto
        {
            Name = "  QC Sửa chữa V2  ",
            Description = "  Cập nhật mô tả  ",
            ServiceCategory = "  REPAIR  ",
            IsActive = false
        });
        Assert.Equal(QcChecklistTemplateCommandStatus.Ok, metadataOnlyUpdate.Status);
        Assert.Null(fakeDataService.LastUpdateReplacementItems);
        Assert.Equal("QC Sửa chữa V2", metadataOnlyUpdate.Data!.Name);
        Assert.Equal("Cập nhật mô tả", metadataOnlyUpdate.Data.Description);
        Assert.False(metadataOnlyUpdate.Data.IsActive);
        Assert.Equal(2, metadataOnlyUpdate.Data.Items.Count);

        var replaceItemsUpdate = await service.UpdateAsync(10, new QcTemplateUpsertDto
        {
            Name = "QC Sửa chữa V3",
            Groups = new List<QcTemplateGroupInputDto>
            {
                new() { Name = "Kiểm tra cuối", Items = new List<string> { "Chạy thử" } }
            }
        });
        Assert.Equal(QcChecklistTemplateCommandStatus.Ok, replaceItemsUpdate.Status);
        Assert.NotNull(fakeDataService.LastUpdateReplacementItems);
        var replacedItem = Assert.Single(replaceItemsUpdate.Data!.Items);
        Assert.Equal("Kiểm tra cuối", replacedItem.Category);
        Assert.Equal("Chạy thử", replacedItem.ItemName);
        Assert.Equal(1, replacedItem.SortOrder);

        fakeDataService.DeleteResult = (false, false);
        var notFoundDelete = await service.DeleteAsync(999);
        Assert.Equal(QcChecklistTemplateCommandStatus.NotFound, notFoundDelete.Status);
        Assert.Equal("Không tìm thấy template QC.", notFoundDelete.ErrorMessage);

        fakeDataService.DeleteResult = (true, true);
        var deactivatedDelete = await service.DeleteAsync(10);
        Assert.Equal(QcChecklistTemplateCommandStatus.Ok, deactivatedDelete.Status);
        Assert.NotNull(deactivatedDelete.Data);
        Assert.Equal("Template đang được tham chiếu bởi phiếu QC nên đã chuyển sang ngưng hoạt động.", deactivatedDelete.Data!.Message);
        Assert.Equal(10, deactivatedDelete.Data.Id);
        Assert.False(deactivatedDelete.Data.IsActive);

        var deactivatedJson = JsonSerializer.Serialize(deactivatedDelete.Data, WebJsonOptions);
        using var deactivatedDoc = JsonDocument.Parse(deactivatedJson);
        Assert.Equal("Template đang được tham chiếu bởi phiếu QC nên đã chuyển sang ngưng hoạt động.", deactivatedDoc.RootElement.GetProperty("message").GetString());
        Assert.Equal(10, deactivatedDoc.RootElement.GetProperty("id").GetInt64());
        Assert.False(deactivatedDoc.RootElement.GetProperty("isActive").GetBoolean());

        fakeDataService.DeleteResult = (true, false);
        var deletedResult = await service.DeleteAsync(10);
        Assert.Equal(QcChecklistTemplateCommandStatus.NoContent, deletedResult.Status);
        Assert.Null(deletedResult.Data);
    }

    private sealed class FakePayrollRunDataService : IPayrollRunDataService
    {
        public (IReadOnlyList<PayrollRun> Items, int Total) PagedResult { get; set; } = (Array.Empty<PayrollRun>(), 0);
        public Func<long, string, DateOnly, DateOnly, PayrollRunPreviewResult>? PreviewResultFactory { get; set; }
        public PayrollRunCreateResult CreateResult { get; set; } = new(PayrollRunCreateStatus.BadRequest, "Unconfigured");
        public PayrollRunApproveResult ApproveResult { get; set; } = new(PayrollRunApproveStatus.NotFound, "Unconfigured");

        public int PreviewCallCount { get; private set; }
        public int CreateCallCount { get; private set; }
        public string? LastPeriodCode { get; private set; }
        public DateOnly LastStartDate { get; private set; }
        public DateOnly LastEndDate { get; private set; }
        public long? LastActorId { get; private set; }

        public Task<(IReadOnlyList<PayrollRun> Items, int Total)> GetPagedAsync(
            long? scopedBranchId,
            int page,
            int pageSize,
            CancellationToken ct = default) => Task.FromResult(PagedResult);

        public Task<PayrollRunPreviewResult> PreviewAsync(
            long targetBranchId,
            string periodCode,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken ct = default)
        {
            PreviewCallCount++;
            LastPeriodCode = periodCode;
            LastStartDate = startDate;
            LastEndDate = endDate;

            var result = PreviewResultFactory?.Invoke(targetBranchId, periodCode, startDate, endDate)
                ?? new PayrollRunPreviewResult(periodCode, targetBranchId, "DRAFT", 0m, Array.Empty<PayrollRunPreviewDetail>());
            return Task.FromResult(result);
        }

        public Task<PayrollRunCreateResult> CreateAsync(
            long targetBranchId,
            string periodCode,
            DateOnly startDate,
            DateOnly endDate,
            long? actorId,
            IReadOnlyList<PayrollRunCreateLineInput>? details,
            CancellationToken ct = default)
        {
            CreateCallCount++;
            LastPeriodCode = periodCode;
            LastStartDate = startDate;
            LastEndDate = endDate;
            LastActorId = actorId;
            return Task.FromResult(CreateResult);
        }

        public Task<PayrollRunApproveResult> ApproveAsync(
            long id,
            long? scopedBranchId,
            long? actorId,
            CancellationToken ct = default) => Task.FromResult(ApproveResult);
    }

    private sealed class FakeQcChecklistTemplateDataService : IQcChecklistTemplateDataService
    {
        public Dictionary<long, string> ServiceOrderCategories { get; set; } = new();
        public List<QcChecklistTemplate> Templates { get; } = new();
        public (bool Found, bool Deactivated) DeleteResult { get; set; } = (true, false);
        public int CreateCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }
        public List<QcChecklistTemplateItem>? LastUpdateReplacementItems { get; private set; }

        private long _nextId = 100;

        public Task<string> GetServiceOrderCategoryAsync(long serviceOrderId, CancellationToken ct = default) =>
            Task.FromResult(ServiceOrderCategories.TryGetValue(serviceOrderId, out var cat) ? cat : string.Empty);

        public Task<List<QcChecklistTemplate>> GetTemplatesAsync(
            string normalizedServiceType,
            bool includeInactive,
            CancellationToken ct = default)
        {
            var query = Templates.AsEnumerable();
            if (!includeInactive)
            {
                query = query.Where(x => x.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(normalizedServiceType))
            {
                query = query.Where(x =>
                    string.IsNullOrWhiteSpace(x.ServiceCategory) ||
                    string.Equals(x.ServiceCategory, normalizedServiceType, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult(query.ToList());
        }

        public Task<QcChecklistTemplate?> GetByIdAsync(long id, CancellationToken ct = default) =>
            Task.FromResult(Templates.FirstOrDefault(x => x.Id == id));

        public Task<QcChecklistTemplate> CreateAsync(QcChecklistTemplate entity, CancellationToken ct = default)
        {
            CreateCallCount++;
            entity.Id = ++_nextId;
            long itemId = 1;
            foreach (var item in entity.TemplateItems)
            {
                item.Id = itemId++;
                item.TemplateId = entity.Id;
            }

            Templates.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<QcChecklistTemplate?> UpdateAsync(
            long id,
            string name,
            string? description,
            string? serviceCategory,
            bool? isActive,
            List<QcChecklistTemplateItem>? replacementItems,
            CancellationToken ct = default)
        {
            UpdateCallCount++;
            LastUpdateReplacementItems = replacementItems;

            var existing = Templates.FirstOrDefault(x => x.Id == id);
            if (existing == null)
            {
                return Task.FromResult<QcChecklistTemplate?>(null);
            }

            existing.Name = name;
            existing.Description = description;
            existing.ServiceCategory = serviceCategory;
            if (isActive.HasValue)
            {
                existing.IsActive = isActive.Value;
            }

            if (replacementItems != null)
            {
                existing.TemplateItems = replacementItems;
            }

            return Task.FromResult<QcChecklistTemplate?>(existing);
        }

        public Task<(bool Found, bool Deactivated)> DeleteOrDeactivateAsync(long id, CancellationToken ct = default) =>
            Task.FromResult(DeleteResult);
    }
}
