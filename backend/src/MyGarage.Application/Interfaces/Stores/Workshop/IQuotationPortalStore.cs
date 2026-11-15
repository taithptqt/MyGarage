using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IQuotationPortalStore
{
    int? GetPortalTokenExpiryDays();
    string GetPortalPublicBaseUrl();

    Task<Quotation?> GetQuotationFullForPortalAsync(long quotationId);
    Task<Quotation?> GetQuotationWithCustomerForOtpAsync(long quotationId);
    Task<Quotation?> GetQuotationAsNoTrackingAsync(long quotationId);
    Task<QuotationAccessLink?> GetLatestActiveAccessLinkForUpdateAsync(long quotationId, string activeStatus, DateTime now);
    Task<List<QuotationAccessLink>> GetActiveAccessLinksForUpdateAsync(long quotationId, string activeStatus);
    Task<List<QuotationAccessLink>> GetSiblingActiveAccessLinksForUpdateAsync(long quotationId, string activeStatus, long excludeLinkId);
    void AddQuotationAccessLink(QuotationAccessLink link);
    Task<QuotationAccessLink?> GetAccessLinkByTokenHashForUpdateAsync(string tokenHash);
}
