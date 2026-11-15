using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Payments;
using MyGarage.DTOs.OData;
using MyGarage.Interfaces;

namespace MyGarage.Services;

// Transitional adapter until the EF context and entities move out of Web.
public sealed class InvoiceQueryReader(IGarageContext context) : IInvoiceQueryReader
{
    public IQueryable<InvoiceODataDto> Query(InvoiceScope scope)
    {
        var query = context.Invoices.AsNoTracking();
        if (scope.Kind == InvoiceScopeKind.Branch)
            query = query.Where(x => (x.ServiceOrder != null && x.ServiceOrder.ExecuteBranchId == scope.Id) ||
                                     (x.ServiceOrderId == null && x.RetailOrder != null && x.RetailOrder.BranchId == scope.Id));
        else if (scope.Kind == InvoiceScopeKind.Customer)
            query = query.Where(x => (x.ServiceOrder != null && x.ServiceOrder.CustomerId == scope.Id) ||
                                     (x.RetailOrder != null && x.RetailOrder.CustomerId == scope.Id));
        else if (scope.Kind != InvoiceScopeKind.All)
            return Enumerable.Empty<InvoiceODataDto>().AsQueryable();

        return query.Select(x => new InvoiceODataDto
        {
            Id = x.Id, ServiceOrderId = x.ServiceOrderId, RetailOrderId = x.RetailOrderId,
            BranchId = x.ServiceOrder != null ? x.ServiceOrder.ExecuteBranchId :
                (x.RetailOrder != null ? x.RetailOrder.BranchId : null),
            PayerType = x.PayerType, PayerId = x.PayerId, SubTotalAmount = x.SubTotalAmount,
            TaxAmount = x.TaxAmount, TotalAmount = x.TotalAmount, PaidAmount = x.PaidAmount,
            PaymentStatus = x.PaymentStatus, IssuedAt = x.IssuedAt, CreatedAt = x.CreatedAt
        });
    }
}
