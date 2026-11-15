using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    /// <summary>
    /// Handles workshop intake validation, check-in log initialization, and customer/vehicle resolution for service orders.
    /// </summary>
    public interface IServiceOrderIntakeService
    {
        Task EnsureCheckInRequestExistsAsync(long serviceOrderId);
        Task<ServiceOrder> PrepareWorkshopOrderEntityAsync(WorkshopServiceOrderCreateDto item, long? actorStaffId, long? forcedGroupSessionId);
        Task EnsureBranchOpenForCheckInAsync(long branchId, DateTime checkInTime);
        Task EnsureCustomerExistsAsync(long customerId);
        Task EnsureInsuranceCompanyExistsAsync(long insuranceCompanyId);
    }
}
