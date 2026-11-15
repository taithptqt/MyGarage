using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed class EfServiceOrderDetailStore : IServiceOrderDetailStore
{
    private readonly IGarageContext _context;

    public EfServiceOrderDetailStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServiceOrderDetail>> GetByServiceOrderIdAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return await _context.ServiceOrderDetails
            .AsNoTracking()
            .Where(detail => detail.ServiceOrderId == serviceOrderId)
            .OrderBy(detail => detail.Id)
            .ToListAsync(ct);
    }

    public Task<ServiceOrder?> GetServiceOrderForUpdateAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId, ct);
    }

    public Task<bool> HasWaitingPartsDetailAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return _context.ServiceOrderDetails
            .AsNoTracking()
            .AnyAsync(x =>
                x.ServiceOrderId == serviceOrderId &&
                x.Status != null &&
                x.Status.ToUpper() == nameof(ServiceOrderDetailStatusCode.WAITING_PARTS), ct);
    }

    public Task<long> GetServiceOrderIdByDetailIdAsync(long detailId, CancellationToken ct = default)
    {
        return _context.ServiceOrderDetails
            .AsNoTracking()
            .Where(x => x.Id == detailId)
            .Select(x => x.ServiceOrderId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> ActiveInsuranceCompanyExistsAsync(long insuranceCompanyId, CancellationToken ct = default)
    {
        return _context.InsuranceCompanies
            .AsNoTracking()
            .AnyAsync(company => company.Id == insuranceCompanyId && company.IsDeleted != true, ct);
    }

    public async Task<long?> GetQuotationIdByDetailIdAsync(long quotationDetailId, CancellationToken ct = default)
    {
        return await _context.QuotationDetails
            .AsNoTracking()
            .Where(x => x.Id == quotationDetailId)
            .Select(x => x.QuotationId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<SupplementaryQuotationMeta?> GetSupplementaryQuotationMetaAsync(long quotationDetailId, CancellationToken ct = default)
    {
        return _context.QuotationDetails
            .AsNoTracking()
            .Where(x => x.Id == quotationDetailId)
            .Join(
                _context.Quotations.AsNoTracking(),
                detail => detail.QuotationId,
                quotation => quotation.Id,
                (detail, quotation) => new SupplementaryQuotationMeta(
                    quotation.Id,
                    quotation.ServiceOrderId,
                    quotation.VersionNo,
                    quotation.CreatedAt,
                    quotation.CreatedBy))
            .FirstOrDefaultAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }
}
