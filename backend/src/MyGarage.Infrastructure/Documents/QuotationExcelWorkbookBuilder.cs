using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Common;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Documents;

internal static class QuotationExcelWorkbookBuilder
{
    private static readonly XLColor TitleColor = XLColor.FromHtml("#1F4E78");
    private static readonly XLColor SubtleColor = XLColor.FromHtml("#EAF1FB");
    private static readonly XLColor LightFillColor = XLColor.FromHtml("#F8FBFF");

    public static QuotationExcelExportResult Build(
        Quotation quotation,
        IReadOnlyList<QuotationSupplementarySignal> supplementarySignals)
    {
        var serviceOrder = quotation.ServiceOrder
            ?? throw new InvalidOperationException("Báo giá chưa liên kết lệnh sửa chữa hợp lệ.");

        var rows = BuildRows(quotation, supplementarySignals);
        var initialRows = rows.Where(x => !x.IsSupplementary).ToList();
        var supplementaryRows = rows.Where(x => x.IsSupplementary).ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(QuotationExcelConstants.MainWorksheetName);
        worksheet.Style.Font.FontName = "Arial";
        worksheet.Style.Font.FontSize = 11;
        worksheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        WriteHeaderBlock(worksheet, quotation, serviceOrder);

        var normalizedStatus = (quotation.Status ?? string.Empty).Trim().ToUpperInvariant();
        var isFinalSettlementExport =
            normalizedStatus == "INSURANCE_APPROVED" ||
            normalizedStatus == "PENDING_CUSTOMER" ||
            normalizedStatus == "CUSTOMER_APPROVED";

        var headers = GetColumnHeaders(isFinalSettlementExport);

        var rowCursor = 10;
        var runningNumber = 1;
        var firstHeaderRow = 0;
        var dataRowNumbers = new List<int>();

        WriteSection(
            worksheet,
            "DỊCH VỤ BAN ĐẦU",
            "Không có dịch vụ ban đầu.",
            initialRows,
            SubtleColor,
            headers,
            isFinalSettlementExport,
            ref rowCursor,
            ref runningNumber,
            ref firstHeaderRow,
            dataRowNumbers);

        rowCursor++;

        WriteSection(
            worksheet,
            "DỊCH VỤ PHÁT SINH (ĐÃ DUYỆT BÁO GIÁ BỔ SUNG)",
            "Chưa có dịch vụ phát sinh.",
            supplementaryRows,
            XLColor.FromHtml("#EEF2FF"),
            headers,
            isFinalSettlementExport,
            ref rowCursor,
            ref runningNumber,
            ref firstHeaderRow,
            dataRowNumbers);

        var dataEndRow = rowCursor - 1;
        ConfigureDataColumns(worksheet);

        var summaryEndRow = WriteSummaryBlock(
            worksheet,
            quotation,
            rows,
            dataEndRow + 2,
            isFinalSettlementExport);

        WriteSignatureBlock(worksheet, quotation, serviceOrder, summaryEndRow + 1);
        ApplyProtectionAndLayout(worksheet, isFinalSettlementExport, dataRowNumbers, firstHeaderRow);
        WriteExcelMetaSheet(workbook, quotation.Id, quotation.Code);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"BaoGia_{quotation.Code}_{VietnamTime.Now:yyyyMMddHHmmss}.xlsx";
        return new QuotationExcelExportResult(stream.ToArray(), QuotationExcelConstants.ContentType, fileName);
    }

    private static List<QuotationExcelRow> BuildRows(
        Quotation quotation,
        IReadOnlyList<QuotationSupplementarySignal> supplementarySignals)
    {
        var detailEntities = quotation.QuotationDetails
            .Where(x => !string.Equals(x.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Id)
            .ToList();

        var supplementaryByQuotationDetailId = supplementarySignals
            .GroupBy(x => x.QuotationDetailId)
            .ToDictionary(
                g => g.Key,
                g => g.Any(sd =>
                    sd.IsSupplementary ||
                    IsSupplementaryServiceSource(sd.ServiceSource)));

        var fallbackSupplementaryByQuotation = IsLikelySupplementaryQuotation(quotation.VersionNo, quotation.QuotationType);

        return detailEntities
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
                    Unit = QuotationExcelConstants.ResolveUnit(x.ItemType),
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPriceAtTime,
                    Total = x.FinalAmount,
                    Remark = x.Description ?? string.Empty,
                    InsurancePaid = x.InsuranceCoveredAmount ?? 0m,
                    CustomerPaid = x.CustomerCoveredAmount ?? x.FinalAmount,
                    TaxRate = x.TaxRate,
                    TaxAmount = x.TaxAmount,
                    TaxableBase = Math.Max(0m, x.SubTotal - ResolveQuotationDetailDiscountValue(x)),
                    IsMandatory = x.IsMandatory,
                    IsService = string.Equals(x.ItemType, "SERVICE", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.ItemType, "LABOR", StringComparison.OrdinalIgnoreCase),
                    IsSupplementary = isSupplementary
                };
            })
            .ToList();
    }

    private static void WriteHeaderBlock(IXLWorksheet worksheet, Quotation quotation, ServiceOrder serviceOrder)
    {
        const int totalColumns = QuotationExcelConstants.TotalColumns;
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

        worksheet.Range(1, 1, 1, totalColumns).Merge().Value = branch?.Name ?? "MyGarage";
        worksheet.Range(1, 1, 1, totalColumns).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, totalColumns).Style.Font.FontColor = XLColor.White;
        worksheet.Range(1, 1, 1, totalColumns).Style.Font.FontSize = 16;
        worksheet.Range(1, 1, 1, totalColumns).Style.Fill.BackgroundColor = TitleColor;
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
        worksheet.Range(5, 1, 5, totalColumns).Style.Fill.BackgroundColor = SubtleColor;
        worksheet.Range(5, 1, 5, totalColumns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(5).Height = 24;

        worksheet.Range(6, 1, 6, 6).Merge().Value = $"Mã lệnh sửa chữa: {serviceOrderCode}";
        worksheet.Range(6, 7, 6, totalColumns).Merge().Value = $"Mã báo giá: {quotation.Code}";

        worksheet.Range(7, 1, 7, 6).Merge().Value = $"Biển số: {licensePlate}  |  VIN: {vin}  |  ODO: {odoDisplay}";
        worksheet.Range(7, 7, 7, totalColumns).Merge().Value = $"Trạng thái: {quotation.Status}";

        worksheet.Range(8, 1, 8, 6).Merge().Value = $"Hãng/Dòng xe: {vehicleDisplay}";
        worksheet.Range(8, 7, 8, totalColumns).Merge().Value = $"Khách hàng: {customerName}  |  SĐT: {customerPhone}";

        worksheet.Range(6, 1, 8, totalColumns).Style.Font.Bold = true;
        worksheet.Range(6, 1, 8, totalColumns).Style.Fill.BackgroundColor = LightFillColor;
        worksheet.Range(6, 1, 8, totalColumns).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(6, 1, 8, totalColumns).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static string[] GetColumnHeaders(bool isFinalSettlementExport)
    {
        return isFinalSettlementExport
            ? new[]
            {
                "STT",
                "Mã VT/CV",
                "Hạng mục",
                "Phân loại",
                "ĐVT",
                "Số lượng",
                "Đơn giá",
                "Thành tiền gốc",
                "Ghi chú",
                "Bảo hiểm chi trả",
                "Khách hàng bù"
            }
            : new[]
            {
                "STT",
                "Mã VT/CV",
                "Hạng mục",
                "Phân loại",
                "ĐVT",
                "Số lượng",
                "Đơn giá",
                "Thành tiền",
                "Ghi chú",
                "Bảo hiểm duyệt giá",
                "Ghi chú của GĐV"
            };
    }

    private static void WriteSection(
        IXLWorksheet worksheet,
        string title,
        string emptyMessage,
        List<QuotationExcelRow> sectionRows,
        XLColor sectionColor,
        string[] headers,
        bool isFinalSettlementExport,
        ref int rowCursor,
        ref int runningNumber,
        ref int firstHeaderRow,
        List<int> dataRowNumbers)
    {
        const int totalColumns = QuotationExcelConstants.TotalColumns;

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
        headerRange.Style.Fill.BackgroundColor = TitleColor;
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
            worksheet.Cell(rowCursor, 4).Value = row.IsService ? "Nhân công" : "Phụ tùng";
            worksheet.Cell(rowCursor, 5).Value = row.Unit;
            worksheet.Cell(rowCursor, 6).Value = row.Quantity;
            worksheet.Cell(rowCursor, 7).Value = row.UnitPrice;
            worksheet.Cell(rowCursor, 8).Value = row.Total;
            worksheet.Cell(rowCursor, 9).Value = row.Remark;
            worksheet.Cell(rowCursor, 10).Value = isFinalSettlementExport ? row.InsurancePaid : string.Empty;
            worksheet.Cell(rowCursor, 11).Value = isFinalSettlementExport ? row.CustomerPaid : string.Empty;

            var rowRange = worksheet.Range(rowCursor, 1, rowCursor, totalColumns);
            rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            if (row.IsService)
            {
                rowRange.Style.Fill.BackgroundColor = LightFillColor;
            }

            dataRowNumbers.Add(rowCursor);
            runningNumber++;
            rowCursor++;
        }
    }

    private static void ConfigureDataColumns(IXLWorksheet worksheet)
    {
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
    }

    private static int WriteSummaryBlock(
        IXLWorksheet worksheet,
        Quotation quotation,
        List<QuotationExcelRow> rows,
        int summaryStart,
        bool isFinalSettlementExport)
    {
        const int totalColumns = QuotationExcelConstants.TotalColumns;
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
        worksheet.Range(summaryStart, 1, summaryStart, totalColumns).Style.Fill.BackgroundColor = TitleColor;
        worksheet.Range(summaryStart, 1, summaryStart, totalColumns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(summaryStart, 1, summaryStart, totalColumns).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        var summaryRows = new List<(string Label, string? Formula, decimal? Value, bool IsStrong)>
        {
            ("Tổng chi phí Nhân công", "SUMIFS($H:$H,$A:$A,\">0\",$D:$D,\"Nhân công\")", null, false),
            ("Tổng chi phí Phụ tùng / Vật tư", "SUMIFS($H:$H,$A:$A,\">0\",$D:$D,\"Phụ tùng\")", null, false),
            ("TỔNG TIỀN HÀNG (CHƯA THUẾ)", "SUMIFS($H:$H,$A:$A,\">0\")", null, true)
        };

        foreach (var vat in vatGroups)
        {
            summaryRows.Add(($"Thuế VAT ({vat.TaxRate.ToString("0.##", CultureInfo.InvariantCulture)}%)", null, vat.TaxAmount, false));
        }

        if (vatGroups.Count == 0)
        {
            summaryRows.Add(("Thuế VAT", null, totalVat, false));
        }

        summaryRows.Add(("TỔNG CHI PHÍ SỬA CHỮA (BAO GỒM THUẾ)", null, totalWithVat, true));
        summaryRows.Add(("Bảo hiểm phê duyệt", "SUMIFS($J:$J,$A:$A,\">0\")", null, false));
        summaryRows.Add(("Phí Miễn thường / Chế tài", null, deductibleOrPenalty, false));
        summaryRows.Add(
            isFinalSettlementExport
                ? ("TỔNG KHÁCH HÀNG THANH TOÁN", "SUMIFS($K:$K,$A:$A,\">0\")", null, true)
                : ("TỔNG KHÁCH HÀNG THANH TOÁN (TẠM TÍNH)", null, customerPayable, true));

        var summaryRowCursor = summaryStart + 1;
        for (var i = 0; i < summaryRows.Count; i++)
        {
            var row = summaryRows[i];
            worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 8).Merge().Value = row.Label;
            if (!string.IsNullOrWhiteSpace(row.Formula))
            {
                worksheet.Cell(summaryRowCursor, 9).FormulaA1 = row.Formula;
                worksheet.Range(summaryRowCursor, 9, summaryRowCursor, 11).Merge();
            }
            else
            {
                worksheet.Range(summaryRowCursor, 9, summaryRowCursor, 11).Merge().Value = row.Value ?? 0m;
            }

            worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 11).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 11).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 11).Style.Fill.BackgroundColor = LightFillColor;
            worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            worksheet.Range(summaryRowCursor, 9, summaryRowCursor, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.Range(summaryRowCursor, 9, summaryRowCursor, 11).Style.NumberFormat.Format = "#,##0";
            if (row.IsStrong)
            {
                worksheet.Range(summaryRowCursor, 1, summaryRowCursor, 11).Style.Font.Bold = true;
            }

            summaryRowCursor++;
        }

        return summaryRowCursor;
    }

    private static void WriteSignatureBlock(
        IXLWorksheet worksheet,
        Quotation quotation,
        ServiceOrder serviceOrder,
        int signatureTitleRow)
    {
        var branch = serviceOrder.ExecuteBranch;
        var preparedBy = quotation.ServiceAdvisor?.FullName
            ?? serviceOrder.ServiceAdvisor?.FullName
            ?? serviceOrder.Receptionist?.FullName
            ?? quotation.CreatedByNavigation?.FullName
            ?? "................................";
        var workshopManager = branch?.Manager?.FullName ?? "................................";
        var exportedAtText = VietnamTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"));

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
        worksheet.Range(signatureLineRow, 1, signatureLineRow + 2, 11).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        worksheet.Range(signatureLineRow, 1, signatureLineRow + 2, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        worksheet.Range(signatureLineRow, 1, signatureLineRow + 2, 11).Style.Alignment.WrapText = true;

        worksheet.Range(signatureLineRow, 1, signatureLineRow + 2, 4).Value =
            $"Họ tên: {preparedBy}\nNgày in: {exportedAtText}";
        worksheet.Range(signatureLineRow, 5, signatureLineRow + 2, 8).Value =
            $"Họ tên: {workshopManager}\nNgày: ........../........../..........";
        worksheet.Range(signatureLineRow, 9, signatureLineRow + 2, 11).Value =
            $"Đại diện ký xác nhận\nHọ tên: ................................\nNgày: ........../........../..........";
    }

    private static void ApplyProtectionAndLayout(
        IXLWorksheet worksheet,
        bool isFinalSettlementExport,
        List<int> dataRowNumbers,
        int firstHeaderRow)
    {
        if (!isFinalSettlementExport && dataRowNumbers.Count > 0)
        {
            foreach (var dataRow in dataRowNumbers)
            {
                worksheet.Cell(dataRow, 10).Style.Protection.SetLocked(false);
                worksheet.Cell(dataRow, 11).Style.Protection.SetLocked(false);
                worksheet.Cell(dataRow, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF8E7");
                worksheet.Cell(dataRow, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF8E7");
            }
        }

        // Always protect the worksheet.
        // - Negotiation mode: only the two GDV input columns on data rows are unlocked above.
        // - Final settlement mode: full sheet is read-only to preserve final figures/formulas.
        worksheet.Protect("mygarage");

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
    }

    private static void WriteExcelMetaSheet(XLWorkbook workbook, long quotationId, string? quotationCode)
    {
        var metaSheet = workbook.Worksheets.Add(QuotationExcelConstants.MetaWorksheetName);
        metaSheet.Cell(1, 1).Value = QuotationExcelConstants.ExportMarker;
        metaSheet.Cell(2, 1).Value = "QuotationId";
        metaSheet.Cell(2, 2).Value = quotationId;
        metaSheet.Cell(3, 1).Value = "QuotationCode";
        metaSheet.Cell(3, 2).Value = quotationCode ?? string.Empty;
        metaSheet.Cell(4, 1).Value = "ExportedAt";
        metaSheet.Cell(4, 2).Value = VietnamTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        metaSheet.Visibility = XLWorksheetVisibility.VeryHidden;
    }

    private static decimal ResolveQuotationDetailDiscountValue(QuotationDetail detail)
    {
        if (detail == null)
        {
            return 0m;
        }

#pragma warning disable CS0618
        return detail.DiscountValue < 0m ? 0m : detail.DiscountValue;
#pragma warning restore CS0618
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
}
