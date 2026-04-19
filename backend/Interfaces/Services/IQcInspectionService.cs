using MyGarage.Models;
using MyGarage.DTOs.Requests;

namespace MyGarage.Interfaces
{
    public interface IQcInspectionService : IGenericService<QcInspection>
    {
        Task<bool> PassAsync(long id, QcPassRequestDto request);
        Task<bool> FailAsync(long id, QcFailRequestDto request);
    }
}
