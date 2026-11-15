using System.Collections.Generic;
using System.IO;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Documents;

public sealed class ClosedXmlQuotationExcelService : IQuotationExcelService
{
    public QuotationExcelExportResult ExportQuotation(
        Quotation quotation,
        IReadOnlyList<QuotationSupplementarySignal> supplementarySignals)
    {
        return QuotationExcelWorkbookBuilder.Build(quotation, supplementarySignals);
    }

    public QuotationExcelImportResult ParseSplitBillingImport(
        Stream excelStream,
        Quotation quotation)
    {
        return QuotationExcelImportParser.Parse(excelStream, quotation);
    }
}
