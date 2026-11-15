using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Documents;

internal static class QuotationExcelImportParser
{
    public static QuotationExcelImportResult Parse(Stream excelStream, Quotation quotation)
    {
        List<QuotationImportRow> importedRows;
        try
        {
            using var workbook = new XLWorkbook(excelStream);
            if (!TryResolveImportWorksheet(workbook, quotation.Id, out var worksheet, out var validationMessage))
            {
                return QuotationExcelImportResult.Failure(validationMessage ?? "File Excel không đúng mẫu import của hệ thống.");
            }

            importedRows = ParseImportedQuotationRows(worksheet);
        }
        catch (InvalidOperationException ex)
        {
            return QuotationExcelImportResult.Failure(ex.Message);
        }
        catch (Exception)
        {
            return QuotationExcelImportResult.Failure("Không thể đọc file Excel. Vui lòng kiểm tra lại định dạng file xuất.");
        }

        var expectedRows = BuildExpectedRows(quotation);

        if (importedRows.Count == 0)
        {
            return QuotationExcelImportResult.Failure("Không tìm thấy dòng dữ liệu hợp lệ trong file Excel.");
        }

        if (importedRows.Count != expectedRows.Count)
        {
            return QuotationExcelImportResult.Failure(
                $"Số dòng trong file ({importedRows.Count}) không khớp báo giá hiện tại ({expectedRows.Count}). Vui lòng xuất lại file mới nhất trước khi nhập.");
        }

        var rowMismatchError = ValidateRowAlignment(expectedRows, importedRows);
        if (rowMismatchError != null)
        {
            return QuotationExcelImportResult.Failure(rowMismatchError);
        }

        var updateItems = BuildUpdateItems(expectedRows, importedRows);
        return QuotationExcelImportResult.Success(updateItems);
    }

    private static List<QuotationExpectedRow> BuildExpectedRows(Quotation quotation)
    {
        return quotation.QuotationDetails
            .Where(x => !string.Equals(x.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Id)
            .Select((x, index) => new QuotationExpectedRow
            {
                Index = index + 1,
                QuotationDetailId = x.Id,
                SkuCode = x.ItemId.HasValue ? x.ItemId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                ItemName = QuotationExcelConstants.NormalizeItemName(x.ItemName),
                Unit = QuotationExcelConstants.ResolveUnit(x.ItemType),
                Quantity = x.Quantity,
                UnitPrice = x.UnitPriceAtTime,
                Total = x.FinalAmount
            })
            .ToList();
    }

    private static string? ValidateRowAlignment(
        List<QuotationExpectedRow> expectedRows,
        List<QuotationImportRow> importedRows)
    {
        for (var i = 0; i < expectedRows.Count; i++)
        {
            var expected = expectedRows[i];
            var imported = importedRows[i];

            if (imported.Stt != expected.Index)
            {
                return $"STT dòng {i + 1} không khớp dữ liệu gốc.";
            }

            if (!StringEqualsNormalized(imported.SkuCode, expected.SkuCode))
            {
                return $"Mã VT/CV ở dòng STT {expected.Index} không khớp dữ liệu gốc.";
            }

            if (!StringEqualsNormalized(imported.ItemName, expected.ItemName))
            {
                return $"Hạng mục ở dòng STT {expected.Index} không khớp dữ liệu gốc.";
            }

            if (!StringEqualsNormalized(imported.Unit, expected.Unit))
            {
                return $"ĐVT ở dòng STT {expected.Index} không khớp dữ liệu gốc.";
            }

            if (!NumericEquals(imported.Quantity, expected.Quantity))
            {
                return $"Số lượng ở dòng STT {expected.Index} không khớp dữ liệu gốc.";
            }

            if (!NumericEquals(imported.UnitPrice, expected.UnitPrice))
            {
                return $"Đơn giá ở dòng STT {expected.Index} không khớp dữ liệu gốc.";
            }

            if (!NumericEquals(imported.Total, expected.Total))
            {
                return $"Thành tiền ở dòng STT {expected.Index} không khớp dữ liệu gốc.";
            }
        }

        return null;
    }

    private static List<QuotationSplitBillingUpdateItemDto> BuildUpdateItems(
        List<QuotationExpectedRow> expectedRows,
        List<QuotationImportRow> importedRows)
    {
        return expectedRows
            .Zip(importedRows, (expected, imported) =>
            {
                var maxInsurance = Math.Max(0m, expected.Total);
                var insurance = imported.InsurancePaid;
                if (insurance < 0m) insurance = 0m;
                if (insurance > maxInsurance) insurance = maxInsurance;

                return new QuotationSplitBillingUpdateItemDto
                {
                    QuotationDetailId = expected.QuotationDetailId,
                    InsuranceCoveredAmount = insurance
                };
            })
            .ToList();
    }

    private static bool TryResolveImportWorksheet(
        XLWorkbook workbook,
        long quotationId,
        out IXLWorksheet worksheet,
        out string? validationMessage)
    {
        worksheet = null!;
        validationMessage = null;

        worksheet = workbook.Worksheets.FirstOrDefault(x =>
            string.Equals(x.Name, QuotationExcelConstants.MainWorksheetName, StringComparison.OrdinalIgnoreCase))!;
        if (worksheet == null)
        {
            validationMessage = $"File Excel thiếu sheet '{QuotationExcelConstants.MainWorksheetName}'. Vui lòng dùng file xuất từ hệ thống.";
            return false;
        }

        if (!ValidateWorkbookMeta(workbook, quotationId, out validationMessage))
        {
            return false;
        }

        if (!HasExpectedHeaderRow(worksheet))
        {
            validationMessage = "File Excel không đúng mẫu cột chuẩn của hệ thống.";
            return false;
        }

        return true;
    }

    private static bool ValidateWorkbookMeta(
        XLWorkbook workbook,
        long quotationId,
        out string? validationMessage)
    {
        validationMessage = null;
        var metaSheet = workbook.Worksheets.FirstOrDefault(x =>
            string.Equals(x.Name, QuotationExcelConstants.MetaWorksheetName, StringComparison.OrdinalIgnoreCase));
        if (metaSheet == null)
        {
            validationMessage = "File Excel không đúng mẫu export MyGarage (thiếu metadata). Vui lòng xuất lại file mới từ hệ thống.";
            return false;
        }

        var marker = QuotationExcelConstants.NormalizeText(metaSheet.Cell(1, 1).GetString());
        if (!string.Equals(marker, QuotationExcelConstants.ExportMarker, StringComparison.Ordinal))
        {
            validationMessage = "File Excel không phải mẫu export hợp lệ của MyGarage.";
            return false;
        }

        long exportQuotationId = 0;
        if (metaSheet.Cell(2, 2).TryGetValue<long>(out var idCellValue))
        {
            exportQuotationId = idCellValue;
        }
        else
        {
            long.TryParse(QuotationExcelConstants.NormalizeText(metaSheet.Cell(2, 2).GetString()), out exportQuotationId);
        }

        if (exportQuotationId <= 0)
        {
            validationMessage = "Metadata file Excel không hợp lệ (thiếu QuotationId).";
            return false;
        }

        if (exportQuotationId != quotationId)
        {
            validationMessage = "File Excel thuộc báo giá khác. Vui lòng dùng đúng file của báo giá đang mở.";
            return false;
        }

        return true;
    }

    private static bool HasExpectedHeaderRow(IXLWorksheet worksheet)
    {
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow <= 0)
        {
            return false;
        }

        var maxScanRow = Math.Min(lastRow, 250);
        for (var rowNumber = 1; rowNumber <= maxScanRow; rowNumber++)
        {
            var col1 = QuotationExcelConstants.NormalizeText(worksheet.Cell(rowNumber, 1).GetString());
            var col2 = QuotationExcelConstants.NormalizeText(worksheet.Cell(rowNumber, 2).GetString());
            var col10 = QuotationExcelConstants.NormalizeText(worksheet.Cell(rowNumber, 10).GetString());

            if (!string.Equals(col1, "STT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(col2, "Mã VT/CV", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!col10.Contains("Bảo hiểm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static List<QuotationImportRow> ParseImportedQuotationRows(IXLWorksheet worksheet)
    {
        var rows = new List<QuotationImportRow>();
        var hasAnyInsuranceInput = false;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow <= 0) return rows;

        for (var rowNumber = 1; rowNumber <= lastRow; rowNumber++)
        {
            var sttRaw = worksheet.Cell(rowNumber, 1).GetString();
            if (!TryParseInt(sttRaw, out var stt) || stt <= 0)
            {
                continue;
            }

            var skuCode = worksheet.Cell(rowNumber, 2).GetString();
            var itemName = worksheet.Cell(rowNumber, 3).GetString();
            var unit = worksheet.Cell(rowNumber, 5).GetString();

            if (!TryParseDecimalCell(worksheet.Cell(rowNumber, 6), out var quantity) ||
                !TryParseDecimalCell(worksheet.Cell(rowNumber, 7), out var unitPrice) ||
                !TryParseDecimalCell(worksheet.Cell(rowNumber, 8), out var total))
            {
                throw new InvalidOperationException($"Không đọc được số liệu ở dòng STT {stt}.");
            }

            var insuranceCell = worksheet.Cell(rowNumber, 10);
            decimal insurancePaid = 0m;
            var insuranceRaw = insuranceCell.GetString();
            var hasInsuranceInput = !string.IsNullOrWhiteSpace(insuranceRaw) || !insuranceCell.IsEmpty();
            if (hasInsuranceInput) hasAnyInsuranceInput = true;

            if (!TryParseDecimalCell(insuranceCell, out insurancePaid))
            {
                throw new InvalidOperationException($"Giá trị 'Bảo hiểm đền' ở dòng STT {stt} không hợp lệ.");
            }

            if (!string.IsNullOrWhiteSpace(insuranceRaw) && insurancePaid < 0m)
            {
                throw new InvalidOperationException($"Giá trị 'Bảo hiểm đền' ở dòng STT {stt} không hợp lệ.");
            }

            rows.Add(new QuotationImportRow
            {
                Stt = stt,
                SkuCode = QuotationExcelConstants.NormalizeText(skuCode),
                ItemName = QuotationExcelConstants.NormalizeItemName(itemName),
                Unit = QuotationExcelConstants.NormalizeText(unit),
                Quantity = quantity,
                UnitPrice = unitPrice,
                Total = total,
                InsurancePaid = insurancePaid
            });
        }

        if (rows.Count > 0 && !hasAnyInsuranceInput)
        {
            throw new InvalidOperationException("Cột 'Bảo hiểm đền' đang trống toàn bộ. Vui lòng nhập ít nhất 1 dòng trước khi import.");
        }

        return rows.OrderBy(x => x.Stt).ToList();
    }

    private static bool TryParseInt(string? value, out int result)
    {
        result = 0;
        var normalized = QuotationExcelConstants.NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized)) return false;
        normalized = normalized.Replace(",", string.Empty).Replace(".", string.Empty);
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDecimalCell(IXLCell cell, out decimal value)
    {
        value = 0m;
        if (cell == null) return false;

        if (cell.TryGetValue<decimal>(out var numeric))
        {
            value = numeric;
            return true;
        }

        var raw = cell.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = 0m;
            return true;
        }

        return TryParseDecimal(raw, out value);
    }

    private static bool TryParseDecimal(string? raw, out decimal value)
    {
        value = 0m;
        var normalized = QuotationExcelConstants.NormalizeText(raw)
            .Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("₫", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.GetCultureInfo("vi-VN"), out value))
        {
            return true;
        }

        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        normalized = normalized.Replace(".", string.Empty).Replace(",", ".");
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool StringEqualsNormalized(string? left, string? right)
    {
        return string.Equals(
            QuotationExcelConstants.NormalizeText(left),
            QuotationExcelConstants.NormalizeText(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool NumericEquals(decimal left, decimal right, decimal tolerance = 0.01m)
    {
        return Math.Abs(left - right) <= tolerance;
    }

    private sealed class QuotationExpectedRow
    {
        public int Index { get; set; }
        public long QuotationDetailId { get; set; }
        public string SkuCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    private sealed class QuotationImportRow
    {
        public int Stt { get; set; }
        public string SkuCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public decimal InsurancePaid { get; set; }
    }
}
