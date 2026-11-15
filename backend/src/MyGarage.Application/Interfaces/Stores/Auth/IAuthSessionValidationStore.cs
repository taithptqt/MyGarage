using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Auth
{
    public interface IAuthSessionValidationStore
    {
        Task<AuthCustomerStampSnapshot?> GetCustomerSecurityStampAsync(long customerId, CancellationToken cancellationToken = default);
        Task<AuthStaffStampSnapshot?> GetStaffSecurityStampAsync(long staffId, CancellationToken cancellationToken = default);

        Task<List<AuthBranchGeoSnapshot>> GetActiveBranchesAsync();
        Task<string> GetBranchNameAsync(long? branchId);
        Task<(string RoleCode, string RoleName)> GetStaffRoleInfoAsync(long? roleId);
    }
}
