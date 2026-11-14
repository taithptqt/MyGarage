using MyGarage.DTOs.OData;

namespace MyGarage.Application.Payments;

public enum InvoiceScopeKind { None, All, Branch, Customer }
public readonly record struct InvoiceScope(InvoiceScopeKind Kind, long? Id);

public interface IInvoiceQueryReader
{
    IQueryable<InvoiceODataDto> Query(InvoiceScope scope);
}

public sealed class InvoiceQuery(IInvoiceQueryReader reader)
{
    public IQueryable<InvoiceODataDto> Execute(InvoiceScope scope)
    {
        if (scope.Kind == InvoiceScopeKind.None ||
            ((scope.Kind == InvoiceScopeKind.Branch || scope.Kind == InvoiceScopeKind.Customer) && !scope.Id.HasValue))
            return Enumerable.Empty<InvoiceODataDto>().AsQueryable();
        return reader.Query(scope);
    }
}
