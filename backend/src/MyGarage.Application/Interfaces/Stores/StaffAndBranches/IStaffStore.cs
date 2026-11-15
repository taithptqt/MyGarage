using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IStaffStore
    {
        Task<Staff?> GetStaffSnapshotByIdAsync(long staffId, CancellationToken cancellationToken = default);
    }
}
