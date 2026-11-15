using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ISharedOperationsAuthorizationStore
    {
        Task<ApprovalRequest?> GetApprovalRequestByIdAsync(long approvalRequestId, CancellationToken ct = default);
        Task<long?> GetGatePassBranchIdAsync(long gatePassId, CancellationToken ct = default);
        Task<long?> GetServiceOrderDiscountBranchIdAsync(long serviceOrderDiscountId, CancellationToken ct = default);
        Task<long?> GetMaterialReturnBranchIdAsync(long materialReturnId, CancellationToken ct = default);
        Task<long?> GetStaffBranchIdAsync(long staffId, CancellationToken ct = default);
    }
}
