using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IPackageService : IGenericService<Package>
    {
        Task<IEnumerable<Package>> GetAllWithItemsAsync();
        Task<Package?> GetByIdWithItemsAsync(long id);
        Task<IEnumerable<PackageResponseDto>> GetAllResponseAsync();
        Task<PackageResponseDto?> GetByIdResponseAsync(long id);
        Task<PackageResponseDto> CreateFromDtoAsync(PackageCreateDto dto);
        Task<PackageResponseDto?> UpdateFromDtoAsync(long id, PackageUpdateDto dto);
    }
}
