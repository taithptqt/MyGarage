using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Execution;

namespace MyGarage.Interfaces
{
    public interface IWorkshopDispatchService
    {
        Task<WorkshopDispatchBoardDto> GetBoardAsync(long branchId, DateTime date);
    }
}
