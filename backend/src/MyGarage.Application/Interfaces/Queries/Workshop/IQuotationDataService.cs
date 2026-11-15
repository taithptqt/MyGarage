using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record QuotationSupplementarySignal(
    long QuotationDetailId,
    bool IsSupplementary,
    string? ServiceSource);

public interface IQuotationDataService
{
    Task<long?> GetOwnerCustomerIdAsync(long quotationId, CancellationToken ct = default);
    Task<bool> QuotationExistsAsync(long quotationId, CancellationToken ct = default);
    Task<bool> SetInsuranceApprovalEvidenceUrlAsync(long quotationId, string? evidenceUrl, CancellationToken ct = default);
    Task<Quotation?> GetForExcelExportAsync(long quotationId, CancellationToken ct = default);
    Task<List<QuotationSupplementarySignal>> GetSupplementarySignalsAsync(
        long serviceOrderId,
        IReadOnlyList<long> detailIds,
        CancellationToken ct = default);
    Task<Quotation?> GetWithDetailsAsNoTrackingAsync(long quotationId, CancellationToken ct = default);
}
