using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderCommissionStore
{
    Task<StaffTierInfo?> GetStaffTierInfoAsync(long staffId);
    Task<bool> HasExistingAdvisorCommissionAsync(long serviceOrderId, long staffId, string sourceType, string logType, string canceledStatus);
    Task<CommissionRule?> GetBestMatchingCommissionRuleAsync(long branchId, string targetType, long staffTierId, long staffId);
    Task<bool> HasCompletedPaymentRecordAsync(long serviceOrderId, string completedStatus);
}
