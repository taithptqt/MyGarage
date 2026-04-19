using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IMaterialReturnService : IGenericService<MaterialReturn>
    {
        Task<MaterialReturn> ProcessReturnAsync(MyGarage.DTOs.Requests.MaterialReturnCreateDto dto);
    }
}
