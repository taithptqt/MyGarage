using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentPackageResolver
    {
        Task<IReadOnlyList<long>> ResolvePackageItemServiceIdsAsync(string? requestedPackageIds);
        Task<List<Package>> LoadRequestedPackagesAsync(string? requestedPackageIds);
    }
}
