using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[Route("api")]
[ApiController]
public class QuotationController : ControllerBase
{
    private readonly IQuotationWorkflowService _quotationWorkflowService;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IGarageContext _context;
    private const long MaxInsuranceEvidenceBytes = 10 * 1024 * 1024;

    public QuotationController(
        IQuotationWorkflowService quotationWorkflowService,
        ICloudStorageService cloudStorageService,
        IGarageContext context)
    {
        _quotationWorkflowService = quotationWorkflowService;
        _cloudStorageService = cloudStorageService;
        _context = context;
    }

    [HttpPost("service-orders/{serviceOrderId:long}/quotations")]
    public async Task<ActionResult<QuotationResponseDto>> CreateQuotation(
        long serviceOrderId,
        [FromBody] QuotationCreateRequestDto request)
    {
        try
        {
            var quotation = await _quotationWorkflowService.CreateQuotationAsync(serviceOrderId, request);
            return Ok(quotation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("service-orders/{serviceOrderId:long}/quotations")]
    public async Task<ActionResult> GetByServiceOrder(long serviceOrderId)
    {
        var rows = await _quotationWorkflowService.GetQuotationsByServiceOrderAsync(serviceOrderId);
        return Ok(rows);
    }

    [HttpGet("service-orders/{serviceOrderId:long}/mandatory-overrides")]
    public async Task<ActionResult> GetMandatoryOverridesByServiceOrder(long serviceOrderId)
    {
        var rows = await _quotationWorkflowService.GetMandatoryOverrideRequestsByServiceOrderAsync(serviceOrderId);
        return Ok(rows);
    }

    [HttpGet("quotations/{quotationId:long}")]
    public async Task<ActionResult<QuotationResponseDto>> GetById(long quotationId)
    {
        var row = await _quotationWorkflowService.GetQuotationAsync(quotationId);
        if (row == null)
        {
            return NotFound(new { message = "Không tìm thấy báo giá." });
        }

        return Ok(row);
    }

    [HttpPost("quotations/{quotationId:long}/approve")]
    public async Task<ActionResult<QuotationResponseDto>> Approve(long quotationId, [FromBody] QuotationApproveRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.ApproveQuotationAsync(quotationId, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/{quotationId:long}/customer-decision")]
    public async Task<ActionResult<QuotationResponseDto>> ApplyCustomerDecision(
        long quotationId,
        [FromBody] QuotationCustomerDecisionRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.ApplyCustomerDecisionAsync(quotationId, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/{quotationId:long}/clone")]
    public async Task<ActionResult<QuotationResponseDto>> CloneQuotation(
        long quotationId,
        [FromBody] QuotationCloneRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.CloneQuotationAsync(quotationId, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/{quotationId:long}/details/{quotationDetailId:long}/cancel")]
    public async Task<ActionResult<QuotationResponseDto>> CancelQuotationDetail(
        long quotationId,
        long quotationDetailId,
        [FromBody] QuotationDetailCancelRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.CancelQuotationDetailAsync(quotationId, quotationDetailId, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/{quotationId:long}/split-billing")]
    public async Task<ActionResult<QuotationResponseDto>> UpdateSplitBilling(
        long quotationId,
        [FromBody] QuotationSplitBillingUpdateRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.UpdateSplitBillingAsync(quotationId, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("mandatory-overrides/{requestId:long}/decision")]
    public async Task<ActionResult> DecideMandatoryOverride(
        long requestId,
        [FromBody] MandatoryOverrideDecisionRequestDto request)
    {
        try
        {
            await _quotationWorkflowService.DecideMandatoryOverrideAsync(requestId, request);
            return Ok(new { message = "Mandatory override updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/submit")]
    public async Task<ActionResult<QuotationResponseDto>> Submit(long quotationId, [FromBody] QuotationStatusUpdateRequestDto request)
    {
        try
        {
            if (!this.IsServiceAdvisorOrReceptionist())
            {
                return Forbid();
            }

            var actorStaffId = ResolveActorStaffId(request?.ActorStaffId);
            if (!actorStaffId.HasValue)
            {
                return BadRequest(new { message = "Thiếu actorStaffId hợp lệ." });
            }

            var updated = await _quotationWorkflowService.SubmitQuotationAsync(quotationId, actorStaffId.Value, request?.Note);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/insurance-approval")]
    public async Task<ActionResult<QuotationResponseDto>> ConfirmInsuranceApproval(
        long quotationId,
        [FromBody] QuotationInsuranceApprovalRequestDto request)
    {
        try
        {
            var normalizedRequest = request ?? new QuotationInsuranceApprovalRequestDto();
            if (!this.IsServiceAdvisorOrReceptionist())
            {
                return Forbid();
            }

            var actorStaffId = ResolveActorStaffId(normalizedRequest.ActorStaffId);
            if (!actorStaffId.HasValue)
            {
                return BadRequest(new { message = "Thiếu actorStaffId hợp lệ." });
            }

            normalizedRequest.ActorStaffId = actorStaffId.Value;
            var updated = await _quotationWorkflowService.ConfirmInsuranceApprovedAsync(quotationId, normalizedRequest);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/insurance-evidence")]
    public async Task<ActionResult<object>> UploadInsuranceEvidence(long quotationId, [FromForm] IFormFile file)
    {
        if (quotationId <= 0)
        {
            return BadRequest(new { message = "Mã báo giá không hợp lệ." });
        }

        if (file == null || file.Length <= 0)
        {
            return BadRequest(new { message = "Vui lòng chọn tệp bằng chứng." });
        }

        if (file.Length > MaxInsuranceEvidenceBytes)
        {
            return BadRequest(new { message = "Tệp bằng chứng vượt quá 10MB." });
        }

        var quotation = await _context.Quotations.FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            return NotFound(new { message = "Không tìm thấy báo giá." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;
        var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        
        await using var stream = file.OpenReadStream();
        var evidenceUrl = await _cloudStorageService.UploadAsync(
            stream,
            $"quotations/{quotationId}/insurance-evidence",
            fileName,
            contentType,
            HttpContext.RequestAborted);

        // Persist to database
        quotation.InsuranceApprovalEvidenceUrl = evidenceUrl;
        quotation.UpdatedAt = VietnamTime.Now;
        await _context.SaveChangesAsync();

        return Ok(new { quotationId, insuranceApprovalEvidenceUrl = evidenceUrl });
    }

    [Authorize]
    [HttpDelete("quotations/{quotationId:long}/insurance-evidence")]
    public async Task<ActionResult> DeleteInsuranceEvidence(long quotationId)
    {
        if (quotationId <= 0)
        {
            return BadRequest(new { message = "Mã báo giá không hợp lệ." });
        }

        var quotation = await _context.Quotations.FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            return NotFound(new { message = "Không tìm thấy báo giá." });
        }

        quotation.InsuranceApprovalEvidenceUrl = null;
        quotation.UpdatedAt = VietnamTime.Now;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã xóa bằng chứng bảo hiểm thành công." });
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/send-to-customer")]
    public async Task<ActionResult<QuotationResponseDto>> SendToCustomer(
        long quotationId,
        [FromBody] QuotationStatusUpdateRequestDto request)
    {
        try
        {
            if (!this.IsServiceAdvisorOrReceptionist())
            {
                return Forbid();
            }

            var actorStaffId = ResolveActorStaffId(request?.ActorStaffId);
            if (!actorStaffId.HasValue)
            {
                return BadRequest(new { message = "Thiếu actorStaffId hợp lệ." });
            }

            var updated = await _quotationWorkflowService.SendQuotationToCustomerAsync(
                quotationId,
                actorStaffId.Value,
                request?.Note,
                request?.IdempotencyKey);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/publish-customer-link")]
    public async Task<ActionResult<QuotationCustomerLinkResponseDto>> PublishCustomerLink(
        long quotationId,
        [FromBody] QuotationCustomerLinkPublishRequestDto request)
    {
        try
        {
            var normalizedRequest = request ?? new QuotationCustomerLinkPublishRequestDto();
            if (!this.IsServiceAdvisorOrReceptionist())
            {
                return Forbid();
            }

            var actorStaffId = ResolveActorStaffId(normalizedRequest.ActorStaffId);
            if (!actorStaffId.HasValue)
            {
                return BadRequest(new { message = "Thiếu actorStaffId hợp lệ." });
            }

            normalizedRequest.ActorStaffId = actorStaffId.Value;
            var result = await _quotationWorkflowService.PublishQuotationCustomerLinkAsync(quotationId, normalizedRequest);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/{quotationId:long}/supersede")]
    public async Task<ActionResult<QuotationResponseDto>> Supersede(long quotationId, [FromBody] QuotationStatusUpdateRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.SupersedeQuotationAsync(quotationId, request?.ActorStaffId ?? 0, request?.Note);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private long? ResolveActorStaffId(long? requestActorStaffId)
    {
        if (requestActorStaffId.HasValue && requestActorStaffId.Value > 0)
        {
            return requestActorStaffId.Value;
        }

        return this.GetCurrentUserId();
    }

    [Authorize]
    [HttpGet("quotations/{quotationId:long}/export/excel")]
    public async Task<IActionResult> ExportQuotationExcel(long quotationId)
    {
        var quotation = await _context.Quotations
            .AsNoTracking()
            .Include(x => x.QuotationDetails)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Customer)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Vehicle)
                    .ThenInclude(x => x.Model)
                        .ThenInclude(x => x.Brand)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.ExecuteBranch)
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation == null)
        {
            return NotFound(new { message = "Không tìm thấy báo giá." });
        }

        var serviceOrder = quotation.ServiceOrder;
        if (serviceOrder == null)
        {
            return BadRequest(new { message = "Báo giá chưa liên kết lệnh sửa chữa hợp lệ." });
        }

        var branch = serviceOrder.ExecuteBranch;
        var vehicle = serviceOrder.Vehicle;
        var model = vehicle?.Model;
        var brand = model?.Brand;
        var customer = serviceOrder.Customer;

        var serviceOrderCode = string.IsNullOrWhiteSpace(serviceOrder.Code)
            ? $"SO-{serviceOrder.Id}"
            : serviceOrder.Code;
        var licensePlate = string.IsNullOrWhiteSpace(vehicle?.LicensePlate) ? "-" : vehicle.LicensePlate;
        var vin = string.IsNullOrWhiteSpace(vehicle?.Vin) ? "-" : vehicle!.Vin!;
        var odoDisplay = vehicle?.Odo?.ToString(CultureInfo.InvariantCulture) ?? "-";
        var vehicleDisplay = string.Join(" ", new[] { brand?.Name, model?.ModelName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(vehicleDisplay))
        {
            vehicleDisplay = "-";
        }
        var customerName = string.IsNullOrWhiteSpace(customer?.FullName) ? "-" : customer.FullName;
        var customerPhone = string.IsNullOrWhiteSpace(customer?.Phone) ? "-" : customer.Phone;

        var detailEntities = quotation.QuotationDetails
            .Where(x => !string.Equals(x.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Id)
            .ToList();

        var detailIds = detailEntities.Select(x => x.Id).ToList();
        var supplementarySignals = await _context.ServiceOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.ServiceOrderId == serviceOrder.Id &&
                x.QuotationDetailId.HasValue &&
                detailIds.Contains(x.QuotationDetailId.Value))
            .Select(x => new
            {
                QuotationDetailId = x.QuotationDetailId!.Value,
                x.IsSupplementary,
                x.ServiceSource
            })
            .ToListAsync();

        var supplementaryByQuotationDetailId = supplementarySignals
            .GroupBy(x => x.QuotationDetailId)
            .ToDictionary(
                g => g.Key,
                g => g.Any(sd =>
                    sd.IsSupplementary ||
                    IsSupplementaryServiceSource(sd.ServiceSource)));

        var fallbackSupplementaryByQuotation = IsLikelySupplementaryQuotation(quotation.VersionNo, quotation.QuotationType);

        var rows = detailEntities
            .Select(x =>
            {
                var hasSupplementarySignal = supplementaryByQuotationDetailId.TryGetValue(x.Id, out var fromServiceOrderDetail);
                var isSupplementary = hasSupplementarySignal ? fromServiceOrderDetail : fallbackSupplementaryByQuotation;

                return new QuotationExcelRow
                {
                    QuotationDetailId = x.Id,
                    ItemType = x.ItemType ?? string.Empty,
                    SkuCode = x.ItemId.HasValue ? x.ItemId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    ItemName = x.ItemName ?? string.Empty,
                    Unit = ResolveUnit(x.ItemType),
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPriceAtTime,
                    Total = x.FinalAmount,
                    Remark = x.Description ?? string.Empty,
                    InsurancePaid = x.InsuranceCoveredAmount ?? 0m,
                    CustomerPaid = x.CustomerCoveredAmount ?? x.FinalAmount,
                    TaxRate = x.TaxRate,
                    TaxAmount = x.TaxAmount,
                    TaxableBase = Math.Max(0m, x.SubTotal - x.DiscountValue),
                    IsMandatory = x.IsMandatory,
                    IsService = string.Equals(x.ItemType, "SERVICE", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.ItemType, "LABOR", StringComparison.OrdinalIgnoreCase),
                    IsSupplementary = isSupplementary
                };
            })
            .ToList();

        var initialRows = rows.Where(x => !x.IsSupplementary).ToList();
        var supplementaryRows = rows.Where(x => x.IsSupplementary).ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("BaoGia");
        worksheet.Style.Font.FontName = "Arial";
        worksheet.Style.Font.FontSize = 11;
        worksheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var totalColumns = 11;
        var titleColor = XLColor.FromHtml("#1F4E78");
        var subtleColor = XLColor.FromHtml("#EAF1FB");

        worksheet.Range(1, 1, 1, totalColumns).Merge().Value = branch?.Name ?? "MyGarage";
        worksheet.Range(1, 1, 1, totalColumns).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, totalColumns).Style.Font.FontColor = XLColor.White;
        worksheet.Range(1, 1, 1, totalColumns).Style.Font.FontSize = 16;
        worksheet.Range(1, 1, 1, totalColumns).Style.Fill.BackgroundColor = titleColor;
        worksheet.Range(1, 1, 1, totalColumns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(1).Height = 26;

        worksheet.Range(2, 1, 2, totalColumns).Merge().Value = branch?.Address ?? string.Empty;
        worksheet.Range(2, 1, 2, totalColumns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(3, 1, 3, totalColumns).Merge().Value = $"Mã số thuế: {branch?.TaxCode ?? "-"}   |   Hotline: {branch?.Hotline ?? "-"}";
        worksheet.Range(3, 1, 3, totalColumns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(3, 1, 3, totalColumns).Style.Font.FontColor = XLColor.FromHtml("#4A5568");

        worksheet.Range(5, 1, 5, totalColumns).Merge().Value = "BẢNG BÁO GIÁ SỬA CHỮA / QUOTATION";
        worksheet.Range(5, 1, 5, totalColumns).Style.Font.Bold = true;
        worksheet.Range(5, 1, 5, totalColumns).Style.Font.FontSize = 14;
        worksheet.Range(5, 1, 5, totalColumns).Style.Fill.BackgroundColor = subtleColor;
        worksheet.Range(5, 1, 5, totalColumns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(5).Height = 24;

        worksheet.Range(6, 1, 6, 6).Merge().Value = $"Mã lệnh sửa chữa: {serviceOrderCode}";
        worksheet.Range(6, 7, 6, totalColumns).Merge().Value = $"Mã báo giá: {quotation.Code}";

        worksheet.Range(7, 1, 7, 6).Merge().Value = $"Biển số: {licensePlate}  |  VIN: {vin}  |  ODO: {odoDisplay}";
        worksheet.Range(7, 7, 7, totalColumns).Merge().Value = $"Trạng thái: {quotation.Status}";

        worksheet.Range(8, 1, 8, 6).Merge().Value = $"Hãng/Dòng xe: {vehicleDisplay}";
        worksheet.Range(8, 7, 8, totalColumns).Merge().Value = $"Khách hàng: {customerName}  |  SĐT: {customerPhone}";

        worksheet.Range(6, 1, 8, totalColumns).Style.Font.Bold = true;
        worksheet.Range(6, 1, 8, totalColumns).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FBFF");
        worksheet.Range(6, 1, 8, totalColumns).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(6, 1, 8, totalColumns).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var sectionHeaderRow = 10;
        var headers = new[]
        {
            "STT",
            "Mã VT/CV",
            "Hạng mục",
            "Ưu tiên",
            "ĐVT",
            "Số lượng",
            "Đơn giá",
            "Thành tiền",
            "Ghi chú",
            "Bảo hiểm đền",
            "Khách bù"
        };

        var rowCursor = sectionHeaderRow;
        var runningNumber = 1;
        var firstHeaderRow = 0;

        void WriteSection(string title, string emptyMessage, List<QuotationExcelRow> sectionRows, XLColor sectionColor)
        {
            worksheet.Range(rowCursor, 1, rowCursor, totalColumns).Merge().Value = title;
            worksheet.Range(rowCursor, 1, rowCursor, totalColumns).Style.Font.Bold = true;
            worksheet.Range(rowCursor, 1, rowCursor, totalColumns).Style.Font.FontColor = XLColor.FromHtml("#1A2332");
            worksheet.Range(rowCursor, 1, rowCursor, totalColumns).Style.Fill.BackgroundColor = sectionColor;
            worksheet.Range(rowCursor, 1, rowCursor, totalColumns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            worksheet.Range(rowCursor, 1, rowCursor, totalColumns).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rowCursor++;

            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(rowCursor, i + 1).Value = headers[i];
            }

            var headerRange = worksheet.Range(rowCursor, 1, rowCursor, totalColumns);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = titleColor;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.WrapText = true;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            worksheet.Row(rowCursor).Height = 22;
            if (firstHeaderRow == 0) firstHeaderRow = rowCursor;
            rowCursor++;

            if (sectionRows.Count == 0)
            {
                worksheet.Range(rowCursor, 1, rowCursor, totalColumns).Merge().Value = emptyMessage;
                worksheet.Range(rowCursor, 1, rowCursor, totalColumns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Range(rowCursor, 1, rowCursor, totalColumns).Style.Font.Italic = true;
                worksheet.Range(rowCursor, 1, rowCursor, totalColumns).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowCursor++;
                return;
            }

            foreach (var row in sectionRows)
            {
                worksheet.Cell(rowCursor, 1).Value = runningNumber;
                worksheet.Cell(rowCursor, 2).Value = row.SkuCode;
                worksheet.Cell(rowCursor, 3).Value = row.IsService ? row.ItemName : $"└─ {row.ItemName}";
                worksheet.Cell(rowCursor, 4).Value = row.IsMandatory ? "Bắt buộc" : "Tùy chọn";
                worksheet.Cell(rowCursor, 5).Value = row.Unit;
                worksheet.Cell(rowCursor, 6).Value = row.Quantity;
                worksheet.Cell(rowCursor, 7).Value = row.UnitPrice;
                worksheet.Cell(rowCursor, 8).Value = row.Total;
                worksheet.Cell(rowCursor, 9).Value = row.Remark;
                worksheet.Cell(rowCursor, 10).Value = row.InsurancePaid;
                worksheet.Cell(rowCursor, 11).Value = row.CustomerPaid;

                var rowRange = worksheet.Range(rowCursor, 1, rowCursor, totalColumns);
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                if (row.IsService)
                {
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FBFF");
                }
                runningNumber++;
                rowCursor++;
            }
        }

        WriteSection(
            "DỊCH VỤ BAN ĐẦU",
            "Không có dịch vụ ban đầu.",
            initialRows,
            XLColor.FromHtml("#EAF1FB"));

        rowCursor++;

        WriteSection(
            "DỊCH VỤ PHÁT SINH (ĐÃ DUYỆT BÁO GIÁ BỔ SUNG)",
            "Chưa có dịch vụ phát sinh.",
            supplementaryRows,
            XLColor.FromHtml("#EEF2FF"));

        var dataEndRow = rowCursor - 1;

        worksheet.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Column(4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Column(5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Column(6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Column(7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Column(8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Column(10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Column(11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        worksheet.Column(6).Style.NumberFormat.Format = "#,##0.##";
        worksheet.Column(7).Style.NumberFormat.Format = "#,##0";
        worksheet.Column(8).Style.NumberFormat.Format = "#,##0";
        worksheet.Column(10).Style.NumberFormat.Format = "#,##0";
        worksheet.Column(11).Style.NumberFormat.Format = "#,##0";

        var summaryStart = dataEndRow + 2;
        var laborBeforeVat = rows.Where(x => x.IsService).Sum(x => x.TaxableBase);
        var partsBeforeVat = rows.Where(x => !x.IsService).Sum(x => x.TaxableBase);
        var totalBeforeVat = rows.Sum(x => x.TaxableBase);
        var totalVat = rows.Sum(x => x.TaxAmount);
        var totalWithVat = quotation.FinalAmount > 0m ? quotation.FinalAmount : totalBeforeVat + totalVat;
        var insuranceCompensation = quotation.TotalInsuranceAmount;
        var baselineCustomerPayable = Math.Max(totalWithVat - insuranceCompensation, 0m);
        var customerPayable = quotation.TotalCustomerAmount > 0m ? quotation.TotalCustomerAmount : baselineCustomerPayable;
        var deductibleOrPenalty = customerPayable - baselineCustomerPayable;
        var vatGroups = rows
            .GroupBy(x => x.TaxRate)
            .Select(g => new { TaxRate = g.Key, TaxAmount = g.Sum(x => x.TaxAmount) })
            .OrderBy(x => x.TaxRate)
            .ToList();

        worksheet.Range(summaryStart, 1, summaryStart, totalColumns).Merge().Value = "TỔNG KẾT CHI PHÍ SỬA CHỮA / QUOTATION SUMMARY";
        worksheet.Range(summaryStart, 1, summaryStart, totalColumns).Style.Font.Bold = true;
        worksheet.Range(summaryStart, 1, summaryStart, totalColumns).Style.Font.FontColor = XLColor.White;
        worksheet.Range(summaryStart, 1, summaryStart, totalColumns).Style.Fill.BackgroundColor = titleColor;
        worksheet.Range(summaryStart, 1, summaryStart, totalColumns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(summaryStart, 1, summaryStart, totalColumns).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        var summaryRows = new List<(string Label, decimal Value, bool IsStrong)>
        {
            ("Tổng chi phí Nhân công", laborBeforeVat, false),
            ("Tổng chi phí Phụ tùng / Vật tư", partsBeforeVat, false),
            ("TỔNG TIỀN HÀNG (CHƯA THUẾ)", totalBeforeVat, true)
        };

        foreach (var vat in vatGroups)
        {
            summaryRows.Add(($"Thuế VAT ({vat.TaxRate.ToString("0.##", CultureInfo.InvariantCulture)}%)", vat.TaxAmount, false));
        }

        if (vatGroups.Count == 0)
        {
            summaryRows.Add(("Thuế VAT", totalVat, false));
        }

        summaryRows.Add(("TỔNG CHI PHÍ SỬA CHỮA (BAO GỒM THUẾ)", totalWithVat, true));
        summaryRows.Add(("Bảo hiểm phê duyệt", -insuranceCompensation, false));
        summaryRows.Add(("Phí Miễn thường / Chế tài", deductibleOrPenalty, false));
        summaryRows.Add(("TỔNG KHÁCH HÀNG THANH TOÁN", customerPayable, true));

        var summaryRowCursor = summaryStart + 1;
        for (var i = 0; i < summaryRows.Count; i++)
        {
            var row = summaryRows[i];
            worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 8).Merge().Value = row.Label;
            worksheet.Range(summaryRowCursor, 9, summaryRowCursor, 11).Merge().Value = row.Value;
            worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 11).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 11).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FBFF");
            worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            worksheet.Range(summaryRowCursor, 9, summaryRowCursor, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.Range(summaryRowCursor, 9, summaryRowCursor, 11).Style.NumberFormat.Format = "#,##0";
            if (row.IsStrong)
            {
                worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 11).Style.Font.Bold = true;
            }
            summaryRowCursor++;
        }

        var signatureTitleRow = summaryRowCursor + 1;
        var signatureLineRow = signatureTitleRow + 1;

        worksheet.Range(signatureTitleRow, 1, signatureTitleRow, 4).Merge().Value = "Người lập báo giá";
        worksheet.Range(signatureTitleRow, 5, signatureTitleRow, 8).Merge().Value = "Quản đốc xưởng";
        worksheet.Range(signatureTitleRow, 9, signatureTitleRow, 11).Merge().Value = "Xác nhận Khách hàng/GĐV";

        worksheet.Range(signatureTitleRow, 1, signatureTitleRow, 11).Style.Font.Bold = true;
        worksheet.Range(signatureTitleRow, 1, signatureTitleRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        worksheet.Range(signatureLineRow, 1, signatureLineRow + 2, 4).Merge();
        worksheet.Range(signatureLineRow, 5, signatureLineRow + 2, 8).Merge();
        worksheet.Range(signatureLineRow, 9, signatureLineRow + 2, 11).Merge();
        worksheet.Range(signatureLineRow, 1, signatureLineRow + 2, 11).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(signatureLineRow, 1, signatureLineRow + 2, 11).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        worksheet.Column(1).Width = 6;
        worksheet.Column(2).Width = 16;
        worksheet.Column(3).Width = 36;
        worksheet.Column(4).Width = 14;
        worksheet.Column(5).Width = 10;
        worksheet.Column(6).Width = 12;
        worksheet.Column(7).Width = 15;
        worksheet.Column(8).Width = 15;
        worksheet.Column(9).Width = 24;
        worksheet.Column(10).Width = 16;
        worksheet.Column(11).Width = 16;

        if (firstHeaderRow > 0)
        {
            worksheet.SheetView.FreezeRows(firstHeaderRow);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"quotation_{quotation.Code}_{VietnamTime.Now:yyyyMMddHHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static string ResolveUnit(string? itemType)
    {
        var value = itemType?.Trim() ?? string.Empty;
        if (value.Equals("SERVICE", StringComparison.OrdinalIgnoreCase)
            || value.Equals("LABOR", StringComparison.OrdinalIgnoreCase))
        {
            return "Công";
        }

        return "Cái";
    }

    private static bool IsSupplementaryServiceSource(string? serviceSource)
    {
        var value = serviceSource?.Trim().ToUpperInvariant() ?? string.Empty;
        return value == "SUPPLEMENTARY_TECHNICIAN" || value == "SUPPLEMENTARY_CUSTOMER";
    }

    private static bool IsLikelySupplementaryQuotation(int versionNo, string? quotationType)
    {
        if (versionNo > 1) return true;
        var value = quotationType?.Trim().ToUpperInvariant() ?? string.Empty;
        return value.Contains("SUPPLEMENTARY", StringComparison.Ordinal);
    }

    private sealed class QuotationExcelRow
    {
        public long QuotationDetailId { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public string Remark { get; set; } = string.Empty;
        public decimal InsurancePaid { get; set; }
        public decimal CustomerPaid { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TaxableBase { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsService { get; set; }
        public bool IsSupplementary { get; set; }
    }

    [HttpGet("quotations/public/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QuotationPublicViewResponseDto), 200)]
    public async Task<ActionResult<QuotationPublicViewResponseDto>> GetByPublicToken(string token)
    {
        try
        {
            var data = await _quotationWorkflowService.GetQuotationByPublicTokenAsync(token);
            return Ok(data);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/public/{token}/decision")]
    [AllowAnonymous]
    public async Task<ActionResult<QuotationResponseDto>> ApplyDecisionByPublicToken(
        string token,
        [FromBody] QuotationPublicDecisionRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.ApplyCustomerDecisionByPublicTokenAsync(token, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/public/{token}/otp/send")]
    [AllowAnonymous]
    public async Task<ActionResult<QuotationPublicOtpResponseDto>> SendDecisionOtpByPublicToken(
        string token,
        [FromBody] QuotationPublicOtpRequestDto? request)
    {
        try
        {
            var response = await _quotationWorkflowService.SendDecisionOtpByPublicTokenAsync(token, request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
