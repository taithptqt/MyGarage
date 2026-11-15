using System.Collections.Generic;
using System.IO;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.Models;

namespace MyGarage.Interfaces;

public sealed record QuotationExcelExportResult(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record QuotationExcelImportResult(
    bool IsValid,
    string? ErrorMessage,
    List<QuotationSplitBillingUpdateItemDto> Items)
{
    public static QuotationExcelImportResult Success(List<QuotationSplitBillingUpdateItemDto> items)
        => new(true, null, items);

    public static QuotationExcelImportResult Failure(string errorMessage)
        => new(false, errorMessage, []);
}

public interface IQuotationExcelService
{
    QuotationExcelExportResult ExportQuotation(
        Quotation quotation,
        IReadOnlyList<QuotationSupplementarySignal> supplementarySignals);

    QuotationExcelImportResult ParseSplitBillingImport(
        Stream excelStream,
        Quotation quotation);
}
