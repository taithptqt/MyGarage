using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    /// <summary>
    /// Orchestrates workshop batch creation and workshop editing workflows for service orders.
    /// </summary>
    public interface IServiceOrderWorkshopWorkflowService
    {
        Task<ServiceOrderBatchCreateResult> AddWorkshopBatchAsync(
            long? actorUserId,
            ServiceOrderBatchCreateRequestDto dto,
            Func<ServiceOrder, Task<ServiceOrder>> createOrderAsync);

        Task<ServiceOrder?> UpdateWorkshopAsync(
            long id,
            WorkshopServiceOrderEditRequestDto dto,
            long? actorUserId = null);
    }
}
