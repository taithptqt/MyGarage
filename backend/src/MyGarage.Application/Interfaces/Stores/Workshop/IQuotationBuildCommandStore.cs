using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IQuotationBuildCommandStore
{
    Task SaveChangesInTransactionAsync();
    Task<T> ExecuteTransactionalAsync<T>(Func<Task<T>> action);
    Task<T> ExecuteCreateQuotationTransactionalAsync<T>(long serviceOrderId, Func<Task<T>> action);

    Task<QuotationServiceOrderAdvisorInfo?> GetServiceOrderAdvisorInfoAsync(long serviceOrderId);
    Task<int> GetMaxQuotationVersionNoAsync(long serviceOrderId);
    Task<List<Quotation>> GetPriorOpenQuotationsForUpdateAsync(long serviceOrderId, string statusSuperseded, string statusCustomerApproved);
    void AddQuotation(Quotation quotation);

    Task<ServiceOrder?> GetServiceOrderAsNoTrackingAsync(long serviceOrderId);
    Task<bool> ServiceOrderExistsAsync(long serviceOrderId);
    Task<Quotation> GetQuotationRequiredForUpdateAsync(long quotationId);
}
