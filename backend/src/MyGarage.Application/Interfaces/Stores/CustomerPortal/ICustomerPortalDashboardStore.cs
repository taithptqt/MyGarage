using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.CustomerPortal.DTOs;
using MyGarage.DTOs.Responses;

namespace MyGarage.Application.CustomerPortal;

public interface ICustomerPortalDashboardStore
{
    Task<CustomerPortalDashboardSummary> GetDashboardSummaryAsync(long customerId, DateTime now, CancellationToken ct = default);

    Task<CustomerPortalDashboardDto> GetDashboardAsync(long customerId, CancellationToken ct = default);

    Task<CustomerPortalServiceOrderLinkContextDto?> GetServiceOrderLinkContextAsync(
        long customerId,
        long serviceOrderId,
        CancellationToken ct = default);
}
