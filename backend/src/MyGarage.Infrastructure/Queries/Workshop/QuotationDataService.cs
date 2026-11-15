using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfQuotationDataService(IGarageContext db) : IQuotationDataService
{
    public Task<long?> GetOwnerCustomerIdAsync(long quotationId, CancellationToken ct = default) =>
        db.Quotations
            .AsNoTracking()
            .Where(q => q.Id == quotationId)
            .Select(q => q.ServiceOrder != null ? (long?)q.ServiceOrder.CustomerId : null)
            .FirstOrDefaultAsync(ct);

    public Task<bool> QuotationExistsAsync(long quotationId, CancellationToken ct = default) =>
        db.Quotations.AsNoTracking().AnyAsync(x => x.Id == quotationId, ct);

    public async Task<bool> SetInsuranceApprovalEvidenceUrlAsync(long quotationId, string? evidenceUrl, CancellationToken ct = default)
    {
        var quotation = await db.Quotations.FirstOrDefaultAsync(x => x.Id == quotationId, ct);
        if (quotation == null)
        {
            return false;
        }

        quotation.InsuranceApprovalEvidenceUrl = evidenceUrl;
        quotation.UpdatedAt = VietnamTime.Now;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<Quotation?> GetForExcelExportAsync(long quotationId, CancellationToken ct = default) =>
        db.Quotations
            .AsNoTracking()
            .Include(x => x.QuotationDetails)
            .Include(x => x.ServiceAdvisor)
            .Include(x => x.CreatedByNavigation)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Customer)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.ServiceAdvisor)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Receptionist)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Vehicle)
                    .ThenInclude(x => x.Model)
                        .ThenInclude(x => x.Brand)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.ExecuteBranch)
                    .ThenInclude(x => x.Manager)
            .FirstOrDefaultAsync(x => x.Id == quotationId, ct);

    public Task<List<QuotationSupplementarySignal>> GetSupplementarySignalsAsync(
        long serviceOrderId,
        IReadOnlyList<long> detailIds,
        CancellationToken ct = default) =>
        db.ServiceOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.ServiceOrderId == serviceOrderId &&
                x.QuotationDetailId.HasValue &&
                detailIds.Contains(x.QuotationDetailId.Value))
            .Select(x => new QuotationSupplementarySignal(
                x.QuotationDetailId!.Value,
                x.IsSupplementary,
                x.ServiceSource))
            .ToListAsync(ct);

    public Task<Quotation?> GetWithDetailsAsNoTrackingAsync(long quotationId, CancellationToken ct = default) =>
        db.Quotations
            .AsNoTracking()
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId, ct);
}
